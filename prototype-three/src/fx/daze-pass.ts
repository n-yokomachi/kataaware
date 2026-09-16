import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const DazeShader = {
  name: 'DazeShader',
  uniforms: {
    tDiffuse: { value: null },
    blur: { value: 0 },
    wobble: { value: 0 },
    time: { value: 0 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform float blur;
    uniform float wobble;
    uniform float time;
    varying vec2 vUv;
    void main() {
      vec2 uv = vUv;
      uv.x += wobble * 0.03 * sin(uv.y * 14.0 + time * 2.5);
      uv.y += wobble * 0.02 * sin(uv.x * 11.0 + time * 1.9);
      float r = blur * 0.012;
      vec4 c = vec4(0.0);
      for (int i = -2; i <= 2; i++) {
        for (int j = -2; j <= 2; j++) {
          c += texture2D(tDiffuse, uv + vec2(float(i), float(j)) * r);
        }
      }
      gl_FragColor = c / 25.0;
    }`,
};

/** ぼかし（blur 0..1）と輪郭の揺れ（wobble 0..1）を数値で持つ後処理 */
export class DazePass extends ShaderPass {
  constructor() {
    super(DazeShader);
    this.enabled = false;
  }

  set(blur: number, wobble: number): void {
    this.uniforms.blur.value = blur;
    this.uniforms.wobble.value = wobble;
    this.enabled = blur > 0 || wobble > 0;
  }

  tick(dt: number): void {
    this.uniforms.time.value += dt;
  }
}
