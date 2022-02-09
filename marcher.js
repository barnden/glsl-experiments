import { WebGL2Renderer } from "./WebGL2.js"

const gl = new WebGL2Renderer("marcher", 60, { preserveDrawingBuffer: false })

gl.addRenderHook()
