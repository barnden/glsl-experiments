#version 300 es
precision highp float;

#define EPSILON 0.0001
#define MAX_T   20.

uniform vec2 u_Resolution;
uniform vec4 u_Mouse;
uniform float u_Time;
uniform int u_Frame;

out vec4 o_FragColor;

// Standard sub/union/intersect
float sd_minus(float d1, float d2) { return max(-d1, d2); }
float sd_union(float d1, float d2) { return min(d1, d2); }
float sd_intersect(float d1, float d2) { return max(d1, d2); }

// Smooth sub/union/intersect
float sm_sub(float d1, float d2, float k)
{
    float h = clamp(.5 + .5 * (d2 - d1) / k, 0., 1.);
    return mix(d2, d1, h) - k * h * (1. - h);
}

float sm_union(float d1, float d2, float k)
{
    float h = clamp(.5 - .5 * (d2 - d1) / k, 0., 1.);
    return mix(d2, -d1, h) - k * h * (1. - h);
}

float sm_intersect(float d1, float d2, float k)
{
    float h = clamp(.5 - .5 * (d2 - d1) / k, 0., 1.);
    return mix(d2, d1, h) - k * h * (1. - h);
}

float op_round(float p, float r) { return p - r; }

// Transform
vec3 op_tx(vec3 pos, mat4 t) { return (inverse(t) * vec4(pos, 1.)).xyz; }

// Rotation
vec3 op_rz(vec3 pos, float deg)
{
    // column major
    mat4 rot = mat4(
        vec4(cos(deg), -sin(deg), 0., 0.),
        vec4(sin(deg), cos(deg), 0., 0.),
        vec4(0., 0., 1., 0.),
        vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

vec3 op_ry(vec3 pos, float deg)
{
    // column major
    mat4 rot = mat4(
        vec4(cos(deg), 0., sin(deg), 0.),
        vec4(0., 1., 0., 0.),
        vec4(-sin(deg), 0., cos(deg), 0.),
        vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

vec3 op_rx(vec3 pos, float deg)
{
    // column major
    mat4 rot = mat4(
        vec4(1., 0., 0., 0.),
        vec4(0., cos(deg), -sin(deg), 0.),
        vec4(0., sin(deg), cos(deg), 0.),
        vec4(0., 0., 0., 1.));

    return op_tx(pos, rot);
}

// Primitives

float sd_octahedron(vec3 pos, float s)
{
    pos = abs(pos);
    float m = pos.x + pos.y + pos.z - s;
    vec3 q;

    if (3. * pos.x < m)
        q = pos.xyz;
    else if (3. * pos.y < m)
        q = pos.yzx;
    else if (3. * pos.z < m)
        q = pos.zxy;
    else
        return m * .57735027;

    float k = clamp(.5 * (q.z - q.y + s), 0., s);
    return length(vec3(q.x, q.y - s + k, q.z - k));
}

float sd_box(vec3 pos, vec3 b)
{
    vec3 q = abs(pos) - b;
    return length(max(q, 0.)) + min(max(q.x, max(q.y, q.z)), 0.);
}

float sd_cylinder(vec3 pos, float h, float r)
{
    vec2 d = abs(vec2(length(pos.yz), pos.x)) - vec2(h, r);
    return min(max(d.x, d.y), 0.) + length(max(d, 0.));
}

float sd_sphere(vec3 pos, float r)
{
    return length(pos) - r;
}

// Composites
float sd_window(vec3 pos, vec3 w)
{
    float q = w.y + .015625;

    return sd_union(
        sd_union(
            sd_box(pos - vec3(0., w.x + .015625, -q), vec3(3., w.xy)),
            sd_box(pos - vec3(0., w.x + .015625, q), vec3(3., w.xy))),
        sd_union(
            sd_box(pos - vec3(0., -(w.z + .015625), -q), vec3(3., w.zy)),
            sd_box(pos - vec3(0., -(w.z + .015625), q), vec3(3., w.zy))));
}

float sd_crown(vec3 pos, vec3 b) { return sd_box(pos, b); }

float sd_fridge(vec3 pos)
{
    vec3 back = vec3(.6, 1.15, .4);
    vec3 front_bottom = vec3(.6, .85, .1) - vec3(.03125);
    vec3 front_top = vec3(.6, .325, .1) - vec3(.03125);

    return sd_union(
        sd_box(pos - vec3(.7, back.y, .5), back),
        sd_union(
            op_round(sd_box(pos - vec3(.7, front_bottom.y, .0), front_bottom), .03125),
            op_round(sd_box(pos - vec3(.7, front_top.y + front_bottom.y + .875, .0), front_top), .03125)));
}

vec4 map(vec3 pos)
{
    // (time, material id, x, x)
    vec4 res = vec4(-1., -1., 0., 1.);

    // assume walls made out of same material
    float wt = .0625; // wt - [w]all [t]hickness
    float ct = wt * 1.3; // ct - [c]rown [t]hickness
    float walls = sd_union(
        sd_union( // front of camera
            sd_union(
                sd_box(pos + vec3(0., -2., 4.) - vec3(wt), vec3(4., 2., wt)), // wall
                sd_crown(pos + vec3(0., -wt, 4. - ct) - vec3(wt), vec3(4., wt, wt / 2.)) // crown moulding
                ),
            sd_union(
                sd_union(
                    sd_box(pos + vec3(4., -2., 0.) - vec3(wt), vec3(wt, 2., 4.)), // wall
                    sd_crown(pos + vec3(4. - ct, -wt, 0.) - vec3(wt), vec3(wt / 2., wt, 4.)) // crown moulding
                    ),
                sd_union(
                    sd_minus( // remove archway in this wall
                        sd_union(
                            sd_box(pos + vec3(2.5, -1. + EPSILON, 2.5) - vec3(wt), vec3(wt * 3., 1.35, 0.85)), // entrance
                            sd_cylinder(pos + vec3(2.5, -2.35, 2.43), .85, wt * 2.2) // entrance arch
                            ),
                        sd_union(
                            sd_box(pos + vec3(2.5, -2., 0.) - vec3(wt), vec3(wt, 2., 4.)), // wall
                            sd_crown(pos + vec3(2.5 - ct, -wt, 0.) - vec3(wt), vec3(wt / 2., wt, 4.)) // crown moulding
                            )),
                    sd_crown(pos + vec3(2.5 - wt, -wt, 3.4 - ct) - vec3(wt), vec3(ct, wt, wt / 2.)) // crown moulding inside archway
                    ))),
        sd_union( // behind camera
            sd_union(
                sd_box(pos - vec3(0., 2., 4.) - vec3(wt), vec3(4., 2., wt)), // wall
                sd_crown(pos - vec3(0., wt, 4. - ct) - vec3(wt), vec3(4., wt, wt / 2.)) // crown moulding
                ),
            sd_minus(
                sd_union( // windows on this wall
                    sd_window(pos - vec3(4., 2.75, 0), vec3(.335, .5, 1.)),
                    sd_union(
                        sd_window(pos - vec3(4., 2.75, -2.75), vec3(.335, .5, 1.)),
                        sd_window(pos - vec3(4., 2.75, 2.75), vec3(.335, .5, 1.)))),
                sd_box(pos - vec3(4., 2., 0.) - vec3(wt), vec3(wt, 2., 4.)))));

    res.xy = vec2(walls, 1.);

    float roof = sd_box(pos - vec3(0., 4., 0.) - vec3(wt, wt, wt), vec3(4., wt, 4.));
    res.x = min(res.x, roof);

    if (res.x > roof)
        res.xy = vec2(roof, 2.);

    float floor = sd_box(pos - vec3(wt, 0., wt), vec3(4., wt, 4.));
    if (res.x > floor)
        res.xy = vec2(floor, 3.);

    float desk = sd_box(pos + vec3(2., -1., 0.) - vec3(wt), vec3(.4, wt / 2., 1.5));

    if (res.x > desk)
        res.xy = vec2(desk, 4.);

    vec3 desk_toy_pos = op_rz(op_ry(pos + vec3(2., -1.5 + .05 * sin(1.2 * u_Time), 1.), u_Time), u_Time * .85);
    float desk_toy = op_round(sd_octahedron(desk_toy_pos, .15), .03125);

    if (res.x > desk_toy)
        res.xy = vec2(desk_toy, 5.);

    float fridge = sd_fridge(pos + vec3(2.5, 0., -3.));

    if (res.x > fridge)
        res.xy = vec2(fridge, 6.);

    return res;
}

vec3 get_normal(vec3 pos)
{
    vec2 e = vec2(EPSILON, 0.);
    return normalize(vec3(map(pos + e.xyy).x - map(pos - e.xyy).x,
                          map(pos + e.yxy).x - map(pos - e.yxy).x,
                          map(pos + e.yyx).x - map(pos - e.yyx).x));
}

mat3 camera(vec3 eye, vec3 lookat, float cr)
{
    vec3 w = normalize(eye - lookat);
    vec3 u = normalize(cross(vec3(sin(cr), cos(cr), 0.), w));
    vec3 v = cross(w, u);

    return mat3(u, v, w);
}

float cast_shadow(vec3 eye, vec3 dir)
{
    float res = 1.;
    float t = EPSILON;

    for (int i = 0; i < 84 && t < MAX_T; i++) {
        float h = map(eye + t * dir).x;

        if (h < EPSILON)
            return 0.;

        res = min(res, 32. * h / t);
        t += h;
    }

    return clamp(res, 0., 1.);
}

vec4 cast_ray(vec3 eye, vec3 dir)
{
    vec3 m = vec3(-1.);
    float t = EPSILON;
    for (int i = 0; i < 172 && t < MAX_T; i++) {
        vec4 h = map(eye + t * dir);

        if (abs(h.x) < EPSILON * t)
            return vec4(t, h.yzw);

        t += h.x;
    }

    return vec4(-1., -1., 0., 1.);
}

float calc_occlusion(vec3 pos, vec3 n)
{
    float occ = 0.;
    float sca = 1.;

    for (int i = 0; i < 5; i++) {
        float h = .01 + .11 * float(i) / 4.;
        float d = map(pos + h * n).x;

        occ += (h - d) * sca;
        sca *= .95;
    }

    return clamp(1. - 2. * occ, 0., 1.);
}

void set_color(vec3 col)
{
    // Gamma correct and set o_FragColor
    col = pow(col, vec3(1. / 2.2));
    o_FragColor = vec4(col, 1.);
}

vec3 get_material(float id)
{
    if (id > 5.5) // fridge
        return vec3(.11, .19, .08);

    if (id > 4.5) // desk toy
        return vec3(.04, .06, .14);

    if (id > 3.5) // desk
        return vec3(.14, .08, .045);

    if (id > 2.5) // floor
        return vec3(.24, .09, .035);

    if (id > 1.5) // roof
        return vec3(.08, .18, .3);

    if (id > .5) // walls
        return vec3(.24, .22, .16);

    // everything else
    return vec3(.18);
}

float get_specular(float id)
{
    if (id > 5.5) // fridge
        return 1.;

    if (id > 4.5) // desk toy
        return 64.;

    if (id > 3.5) // desk
        return 1.;

    if (id > 2.5) // floor
        return 1.;

    // everything else
    return 1.;
}

void main()
{
    vec2 uv = (2. * gl_FragCoord.xy - u_Resolution.xy) / u_Resolution.xy;
    vec2 mc = u_Mouse.xy / u_Resolution.xy;

    vec3 lookat = vec3(.75, 1.75, 1.5);
    vec2 angle = 6.2831 * (vec2(0.055) + u_Mouse.z * u_Mouse.xy / u_Resolution.xy);
    vec3 eye = lookat + vec3(cos(angle.x), .35, sin(angle.x));
    mat3 cam = camera(eye, lookat, 0.);
    vec3 dir = cam * normalize(vec3(uv, -.85));

    vec3 sky = vec3(.3, .45, .85);
    vec3 col = sky - .5 * dir.y;
    vec4 ray = cast_ray(eye, dir);
    float t = ray.x;
    float focc = ray.w;

    if (t <= 0.) {
        set_color(col);
        return;
    }

    vec3 pos = eye + t * dir;
    vec3 N = get_normal(pos);
    vec3 sun_dir = normalize(vec3(.9, .125, .25));
    vec3 R = reflect(sun_dir, N);
    float occ = calc_occlusion(pos, N) * focc;
    float sun_dif = clamp(dot(sun_dir, N), 0., 1.);
    vec3 sun_rfl = normalize(sun_dir - dir);
    float sun_sha = cast_shadow(pos + EPSILON * N, sun_dir);
    float sky_dif = sqrt(clamp(.5 + .5 * N.y, 0., 1.));
    float sun_spc = get_specular(ray.y) * pow(clamp(dot(N, sun_rfl), 0., 1.), 8.) * sun_dif * (.04 + .95 * pow(clamp(1. + dot(sun_rfl, dir), 0., 1.), 5.));
    float bou_dif = sqrt(clamp(.1 - .9 * N.y, 0., 1.)) * clamp(1. - .1 * pos.y, 0., 1.);

    col = vec3(4., 1.85, 0.85) * sun_dif * sun_sha
          + vec3(5., 2., 1.) * sun_spc * sun_sha
          + sky * sky_dif * occ
          + vec3(.5, .2, .1) * bou_dif * occ;
    col *= get_material(ray.y);

    set_color(col);
}
