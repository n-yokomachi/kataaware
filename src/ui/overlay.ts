const wait = (seconds: number): Promise<void> => new Promise((r) => setTimeout(r, seconds * 1000));

/** 字幕、印、中央の文字、暗転、開始と再開の画面 */
export class Overlay {
  private start: HTMLDivElement;
  private resume: HTMLDivElement;
  private fade: HTMLDivElement;
  private subtitle: HTMLDivElement;
  private prompt: HTMLDivElement;
  private center: HTMLDivElement;

  constructor(root: HTMLElement) {
    const make = (id: string, text = ''): HTMLDivElement => {
      const el = document.createElement('div');
      el.id = id;
      el.textContent = text;
      root.appendChild(el);
      return el;
    };
    // 後に追加したものほど手前に重なる
    this.fade = make('fade');
    this.subtitle = make('subtitle');
    this.prompt = make('prompt');
    this.center = make('center');
    this.resume = make('resume', 'クリックで再開');
    this.start = make('start', 'クリックで開始');
    this.resume.classList.add('hidden');
    this.setPrompt(null);
  }

  waitForStart(): Promise<void> {
    return new Promise((resolve) => {
      this.start.addEventListener(
        'click',
        (e) => {
          e.stopPropagation();
          this.start.classList.add('hidden');
          resolve();
        },
        { once: true },
      );
    });
  }

  showResume(show: boolean): void {
    this.resume.classList.toggle('hidden', !show);
  }

  onResume(cb: () => void): void {
    this.resume.addEventListener('click', (e) => {
      e.stopPropagation();
      cb();
    });
  }

  setSubtitle(text: string | null): void {
    this.subtitle.textContent = text ?? '';
  }

  setPrompt(text: string | null): void {
    this.prompt.textContent = text ?? '';
    this.prompt.classList.toggle('hidden', text === null);
  }

  /** 黒い層の不透明度を seconds 秒かけて変える。0 なら即時 */
  fadeTo(opacity: number, seconds: number): Promise<void> {
    this.fade.style.transition = seconds > 0 ? `opacity ${seconds}s linear` : 'none';
    this.fade.style.opacity = String(opacity);
    return wait(seconds);
  }

  /** 中央に文字を出し、seconds 秒見せてから消す */
  async showCenter(text: string, seconds: number): Promise<void> {
    this.center.textContent = text;
    this.center.style.opacity = '1';
    await wait(seconds);
    this.center.style.opacity = '0';
    await wait(0.8);
  }

  /** 中央に文字を出したままにする（結末用） */
  holdCenter(text: string): void {
    this.center.textContent = text;
    this.center.style.opacity = '1';
  }
}
