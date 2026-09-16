import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }`;

const Ps1Shader = {
  name: 'Ps1Shader',
  uniforms: {
    tDiffuse: { value: null },
    levels: { value: 32 },
    dither: { value: 1 },
    amount: { value: 1 },
  },
  vertexShader: VERTEX,
  fragmentShader: /* glsl */ `
    uniform sampler2D tDiffuse;
    uniform float levels;
    uniform float dither;
    uniform float amount;
    varying vec2 vUv;
    // 4x4 の規則的なディザ。gl_FragCoord は低解像度の描画先の画素座標
    float bayer2(vec2 a) {
      a = floor(a);
      return fract(a.x / 2.0 + a.y * a.y * 0.75);
    }
    float bayer4(vec2 a) {
      return bayer2(0.5 * a) * 0.25 + bayer2(a);
    }
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      // 表示色に近い明るさで減色する。線形のまま量子化すると暗部だけ段差が粗くなる
      vec3 g = pow(max(c.rgb, 0.0), vec3(1.0 / 2.2));
      float t = bayer4(gl_FragCoord.xy) - 0.5;
      vec3 q = floor(g * levels + dither * t + 0.5) / levels;
      vec3 back = pow(max(q, 0.0), vec3(2.2));
      gl_FragColor = vec4(mix(c.rgb, back, amount), c.a);
    }`,
};

/** 減色（levels 段階）とディザ（dither 0..1）。amount 0 で素通し */
export class Ps1Pass extends ShaderPass {
  constructor() {
    super(Ps1Shader);
  }

  set(levels: number, dither: number, amount: number): void {
    this.uniforms.levels.value = levels;
    this.uniforms.dither.value = dither;
    this.uniforms.amount.value = amount;
    this.enabled = amount > 0;
  }
}
