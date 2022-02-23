#version 300 es
precision highp float;

uniform vec2 u_Resolution;
uniform vec4 u_Mouse;
uniform float u_Time;
uniform int u_Frame;

out vec4 o_FragColor;

// Consts
#define MAX_ITER                100
#define MAX_TIME                20.
#define EPSILON                 0.0001

#define PI  3.141592653
#define TAU 6.283185307

#define RAY_INVERSION 2

#define SKY pow(vec3(.0833, .55, .75) - .5 * ray.direction.y, vec3(.4545))

// Misc
float length2(vec2 p) { return dot(p, p); }
float length2(vec3 p) { return dot(p, p); }

// PRNG
float rand_interval(vec2 seed)
{
    return fract(sin(dot(seed, vec2(12.9898, 78.233))) * 43758.5453);
}
float rand(vec2 seed) { return 2. * rand_interval(seed) - 1.; }
vec3 rand_sphere(vec3 p)
{
    for (int i = 0; i < 5 && length2(p) >= 1.; i++)
        p = vec3(rand(p.xy), rand(p.zy), rand(p.xz));

    return p;
}

// Raymarching/tracing stuff
struct Ray {
    vec3 origin;
    vec3 direction;
};

struct Light {
    vec3 origin;
    vec3 color;
};

const int DIELECTRIC = 1;
const int LAMBERTIAN = 2;
const int METAL = 3;

struct Material {
    vec3 ka; // color of ambient
    vec3 kd; // color of diffuse
    float ks; // intensity of specular
    float kr; // reflection mix
    float kt; // refraction mix
    float m; // roughness (beckmann)
    float ior; // refract idx
    int scatter; // type of light scattering
};

struct Record {
    bool hit;
    vec3 point;
    vec3 normal;
    Material material;
};

mat3 get_lookat(vec3 eye, vec3 lookat, float r)
{
    vec3 w = normalize(lookat - eye);
    vec3 u = normalize(cross(vec3(cos(r), sin(r), 0.), w));

    return mat3(cross(u, w), u, w);
}

Ray primaryRay(vec3 eye, vec3 ta, float fovy, float focalDistance)
{
    // Perspective
    mat3 lookat = get_lookat(eye, ta, 0.);
    vec2 uv = (2. * gl_FragCoord.xy - u_Resolution.xy) / u_Resolution.xy * tan(radians(fovy) / 2.);

    return Ray(eye, normalize(lookat * vec3(uv, focalDistance)));
}

// SDF Primitives
float sd_sphere(vec3 p, float r) { return length(p) - r; }
float sd_box(vec3 p, vec3 b)
{
    vec3 q = abs(p) - b;
    return length(max(q, 0.)) + min(max(q.x, max(q.y, q.z)), 0.);
}

// SDF Operations
float op_round(float d, float r) { return d - r; }
float op_smunion(float d1, float d2, float k)
{
    float h = clamp(.5 + .5 * (d2 - d1) / k, 0., 1.);
    return mix(d2, d1, h) - k * h * (1. - h);
}

// Transform
vec3 op_tx(vec3 pos, mat4 t) { return (inverse(t) * vec4(pos, 1.)).xyz; }

// Rotation
vec3 op_rz(vec3 pos, float deg)
{
    mat4 rot = mat4(vec4(cos(deg), -sin(deg), 0., 0.), vec4(sin(deg), cos(deg), 0., 0.),
                    vec4(0., 0., 1., 0.), vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

vec3 op_ry(vec3 pos, float deg)
{
    mat4 rot = mat4(vec4(cos(deg), 0., sin(deg), 0.), vec4(0., 1., 0., 0.),
                    vec4(-sin(deg), 0., cos(deg), 0.), vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

vec3 op_rx(vec3 pos, float deg)
{
    mat4 rot = mat4(vec4(1., 0., 0., 0.), vec4(0., cos(deg), -sin(deg), 0.),
                    vec4(0., sin(deg), cos(deg), 0.), vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

void emplace(inout vec2 rec, float d, float idx)
{
    if (rec.x > d)
        rec = vec2(d, idx);
}

vec2 sd_map(vec3 p)
{
    vec2 angle = TAU * mix(.5 * vec2(cos(u_Time / 5.), sin(u_Time / 7.)) + vec2(.335, .465), u_Mouse.z * (u_Mouse.yx / u_Resolution.yx), u_Mouse.z);
    vec2 rec = vec2(MAX_TIME, 1.);

    rec.x = dot(p + vec3(0., 3., 0.), normalize(vec3(0., 1., 1.)));
    emplace(rec, dot(p + vec3(0., 5., 0.), normalize(vec3(0., 5., 0.))), 1.);
    emplace(rec, sd_sphere(p + vec3(0., 0., 1.), .15), 4.);

    vec3 q = op_rx(op_ry(p, -angle.y), angle.x);

    float sphere = sd_sphere(q, .3);
    float cube = sd_box(q, vec3(.15));

    emplace(
        rec,
        op_smunion(mix(sphere, cube, smoothstep(-1., 1., cos(.5 * u_Time))),
                   sd_sphere(p + .4 * vec3(sin(u_Time)) * vec3(1., -1., 1.), .1),
                   .4),
        2.);

    return rec;
}

vec3 get_normal(vec3 p)
{
    vec2 e = vec2(1., -1.) * EPSILON;
    return normalize(e.xyy * sd_map(p + e.xyy).x + e.yyx * sd_map(p + e.yyx).x + e.yxy * sd_map(p + e.yxy).x + e.xxx * sd_map(p + e.xxx).x);
}

Material get_material(float idx)
{
    if (idx > 3.5)
        return Material(vec3(.2), vec3(.23, .42, .26), .2, 0., 0., .05, 2.5, METAL);

    if (idx > 2.5)
        return Material(vec3(.2), vec3(.0), 0., .8, 0., 1., 2.5, METAL);

    if (idx > 1.5)
        return Material(vec3(0.05),
                        vec3(0.), // vec3(.53, .23, .43),
                        .8, .7, .7, .025,
                        1.52, // approx ior for glass
                        DIELECTRIC);

    if (idx > 0.5)
        return Material(vec3(.12), vec3(.2), 0., 0., 0., 1., 2.5, LAMBERTIAN);

    return Material(vec3(0.), vec3(0.), 0., 0., 0., 0., 1., LAMBERTIAN);
}

Record cast_ray(Ray ray, bool inversion)
{
    float t = EPSILON;
    vec2 h;
    vec3 p;
    for (int i = 0; i < MAX_ITER && t < MAX_TIME; i++) {
        p = ray.origin + ray.direction * t;
        h = sd_map(p);

        if (inversion)
            h.x *= -1.;

        if (abs(h.x) < EPSILON)
            break;

        t += h.x;
    }

    if (t < MAX_TIME)
        return Record(true, p, get_normal(p), get_material(h.y));

    return Record(false, vec3(0.), vec3(0.), get_material(0.));
}

// Stuff for specular highlights
// Fresnel approx.
float schlick(float r0, float c) { return r0 + (1. - r0) * pow(1. - c, 5.); }

float beckmann(float HN, float m)
{
    float cos2 = HN * HN;
    float tan2 = (cos2 - 1.) / cos2;
    float m2 = m * m;

    return exp(tan2 / m2) / (PI * m2 * cos2 * cos2);
}

float cook_torrance(vec3 L, vec3 N, vec3 E, float ior1, float ior2, float m)
{
    vec3 H = normalize(L + E);

    float EN = dot(E, N);
    float LN = dot(L, N);
    float HN = max(EPSILON, dot(H, N));

    float c = 2. * HN / dot(E, H);
    float G = min(1., min(c * EN, c * LN));

    float D = beckmann(HN, m);

    float r0 = pow((ior1 - ior2) / (ior1 + ior2), 2.);
    float F = schlick(r0, EN);

    return D * F * G / (PI * EN * LN);
}

// TODO: Make this work with multiple light sources
const Light sun = Light(vec3(-1.25, 1.2, 2.), vec3(.64, .59, .54));

int lambertian(inout Ray ray, Record rec, inout vec3 attenuation)
{
    ray = Ray(rec.point + rec.normal * EPSILON,
              normalize(rec.normal + rand_sphere(rec.point)));

    if (length(rec.material.kr) < EPSILON)
        return 0;

    attenuation *= rec.material.kd;

    return 1;
}

int metal(inout Ray ray, Record rec, inout vec3 attenuation)
{
    ray = Ray(rec.point + rec.normal * EPSILON,
              normalize(reflect(ray.direction, rec.normal)));
    attenuation *= rec.material.kr;

    if (length(rec.material.kr) < EPSILON || dot(ray.direction, rec.normal) < 0.)
        return 0;

    return 1;
}

int dielectric(inout Ray ray, Record rec, inout vec3 attenuation)
{
    vec3 normal = rec.normal;

    float ior;
    float reflect_prob = 1.;
    float cosine = dot(normalize(ray.direction), rec.normal);

    if (dot(ray.direction, rec.normal) > 0.) {
        normal *= -1.;
        ior = rec.material.ior;
    } else {
        ior = 1. / rec.material.ior;
        cosine *= -1.;
    }

    vec3 reflected = normalize(reflect(ray.direction, rec.normal));
    vec3 refracted = normalize(refract(ray.direction, normal, ior));
    if (length2(refracted) > EPSILON) {
        float r0 = (rec.material.ior - 1.) / (rec.material.ior + 1.);
        reflect_prob = schlick(r0 * r0, cosine);
    }

    if (rand_interval(ray.direction.xy) < reflect_prob) {
        ray = Ray(rec.point + rec.normal * EPSILON, reflected);
        attenuation *= rec.material.kr;
        return 1;
    }

    ray = Ray(rec.point - EPSILON * 10. * normal, refracted);
    attenuation *= rec.material.kt;

    return RAY_INVERSION;
}

int get_scatter(inout Ray ray, Record rec, inout vec3 attenuation)
{
    if (rec.material.scatter == LAMBERTIAN)
        return lambertian(ray, rec, attenuation);

    if (rec.material.scatter == DIELECTRIC)
        return dielectric(ray, rec, attenuation);

    return metal(ray, rec, attenuation);
}

vec3 compute_ray_color(Ray ray, Record rec)
{
    if (!rec.hit)
        return SKY;

    vec3 color = rec.material.ka;
    vec3 L = sun.origin - rec.point;
    vec3 N = rec.normal;

    Ray shadow = Ray(rec.point + N * EPSILON, normalize(L));
    if (cast_ray(shadow, false).hit) {
        color += .1 * rec.material.kd * rec.material.ka;
        return color;
    }

    vec3 E = normalize(ray.origin - rec.point);
    float LN = dot(L, N);

    vec3 diffuse = rec.material.kd * max(0., LN);
    float specular = rec.material.ks * cook_torrance(L, N, E, rec.material.ior, 1., rec.material.m);
    color += sun.color * (diffuse + specular);

    return color;
}

vec3 trace_ray(Ray ray)
{
    Record rec = cast_ray(ray, false);

    vec3 color = vec3(0.);
    vec3 attenuation = vec3(1.);
    bool inversion = false;

    for (int i = 0; i < 12; i++) {
        color += attenuation * compute_ray_color(ray, rec);
        int scatter = get_scatter(ray, rec, attenuation);

        if (scatter < 1 || length2(attenuation) < EPSILON)
            break;

        if (scatter == RAY_INVERSION)
            inversion = !inversion;

        rec = cast_ray(ray, inversion);
    }

    return color;
}

void main()
{
    vec3 lookat = vec3(0., 0., -1.);
    vec3 eye = vec3(0., 0., 1.);
    Ray ray = primaryRay(eye, lookat, 90., 1.5);

    o_FragColor = vec4(trace_ray(ray), 1.);
}
