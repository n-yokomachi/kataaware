import { Color } from 'three';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const ToneShader = {
  name: 'ToneShader',
  uniforms: {
    tDiffuse: { value: null },
    color: { value: new Color(1, 1, 1) },
    amount: { value: 0 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform vec3 color;
    uniform float amount;
    varying vec2 vUv;
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      vec3 tinted = c.rgb * color;
      gl_FragColor = vec4(mix(c.rgb, tinted, amount), c.a);
    }`,
};

/** 指定色を掛け合わせた色味へ amount (0..1) だけ寄せる後処理 */
export class TonePass extends ShaderPass {
  constructor() {
    super(ToneShader);
  }

  set(color: number, amount: number): void {
    (this.uniforms.color.value as Color).set(color);
    this.uniforms.amount.value = amount;
  }
}
