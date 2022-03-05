import { WebGL2Renderer } from "./WebGL2.js"

class Marcher {
    constructor() {
        this.renderer = new WebGL2Renderer("marcher", 60, { preserverDrawingBuffer: false })
        this.renderer.addRenderHook(this.render)

        this.setup()

        this.mousePosition = [0, 0]
        this.mouseInfo = [0, 0]

        this.renderer.render()
    }

    setup() {
        const gl = this.renderer.gl

        this.program = this.renderer.createProgram([
            [gl.VERTEX_SHADER, "discard"],
            [gl.FRAGMENT_SHADER, "frag"]
        ])

        this.locations = {
            i_Position: gl.getAttribLocation(this.program, "i_Position"),
            u_Resolution: gl.getUniformLocation(this.program, "u_Resolution"),
            u_Mouse: gl.getUniformLocation(this.program, "u_Mouse"),
            u_Time: gl.getUniformLocation(this.program, "u_Time"),
            u_Frame: gl.getUniformLocation(this.program, "u_Frame")
        }

        let buffer = this.renderer.createBuffer(new Float32Array([
            -1., -1., 0.,
            3., -1., 0.,
            -1., 3., 0.
        ]), gl.STATIC_DRAW)

        this.vao = this.renderer.createVertexArray([[buffer, this.locations.i_Position]])

        gl.bindBuffer(gl.ARRAY_BUFFER, null)
        gl.bindBuffer(gl.TRANSFORM_FEEDBACK_BUFFER, null)

        gl.enable(gl.BLEND)
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA)

        this.setupMouseControls()
        this.setupKeyboardControls()
    }

    setupMouseControls() {
        this.mouseDown = [false, false, false]

        const getCoordinates = e => {
            const coords = (typeof (e.touches) === "undefined") ? e : e.touches[0]

            return [coords.clientX, coords.clientY]
        }

        const moveHandler = e => this.mousePosition = getCoordinates(e)

        const liftHandler = e => {
            if (e.type === "mouseup") {
                this.mouseDown[e.button] = false
                this.mouseInfo = [this.mouseDown.reduce((acc, cur) => acc |= cur), 0]
            } else {
                this.mouseInfo = [0, 0]
            }
        }

        const pressHandler = e => {
            if (e.type === "mousedown") {
                this.mouseDown[e.button] = true
                this.mouseInfo = [this.mouseDown.reduce((acc, cur) => acc |= cur), 0]
            } else {
                this.mouseInfo = [1, 1]
            }

            this.mousePosition = getCoordinates(e)
        }

        this.renderer.canvas.addEventListener("contextmenu", e => e.preventDefault())

        this.renderer.canvas.addEventListener("mousedown", pressHandler)
        this.renderer.canvas.addEventListener("mousemove", moveHandler)
        document.addEventListener("mouseup", liftHandler)

        this.renderer.canvas.addEventListener("touchstart", pressHandler)
        this.renderer.canvas.addEventListener("touchmove", moveHandler)
        document.addEventListener("touchend", liftHandler)
    }

    setupKeyboardControls() {
        document.addEventListener("keydown", e => {
            if (e.key == 'p')
                this.renderer.pauseTime ^= true
        })
    }

    render = gl => {
        gl.useProgram(this.program)
        gl.bindVertexArray(this.vao)

        gl.uniform2f(this.locations.u_Resolution, gl.canvas.width, gl.canvas.height)
        gl.uniform4f(this.locations.u_Mouse, ...this.mousePosition, ...this.mouseInfo)
        gl.uniform1f(this.locations.u_Time, this.renderer.time)
        gl.uniform1i(this.locations.u_Frame, this.renderer.frameCount)

        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height)
        gl.drawArrays(gl.TRIANGLES, 0, 3)
    }
}

export { Marcher }
