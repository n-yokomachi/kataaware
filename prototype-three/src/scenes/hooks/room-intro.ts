import type { Hooks } from '../runtime';

export const CREDITS = ['制作 〔名義〕'];
export const TITLE_CARD = 'HALF AWARE';
export const FIRST_LINE = 'うぅ…今回は酔いが酷い…';
export const AFTER_SMOKE_LINE = '煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう';
/** クレジットとタイトルカードを見せている秒数。showCenter の表示時間と消える時間の合計に合わせる */
export const INTRO_SECONDS = 6.2;
/** 煙草を取ってから吸い終わるまでの秒数 */
export const SMOKE_SECONDS = 4;

/**
 * 眩暈の中でクレジットとタイトルを順に出し、その間は調べる操作を止める。終わったら最初の独白を流す。
 * 煙草を取ったら、煙を立てて数秒止め、吸い終わりの独白を流す。
 * 見た目（中央の文字、煙）は実時間で動くが、操作の停止と独白の時刻は場面のフレーム時間で決める。
 * 場面が終わっていたら途中でやめる。
 */
export function roomIntroHooks(): Hooks {
  let alive = false;
  let introEndsAt: number | null = null;
  let smokeEndsAt: number | null = null;
  return {
    onEnter(ctx, rt) {
      alive = true;
      introEndsAt = rt.time + INTRO_SECONDS;
      rt.freeze(INTRO_SECONDS);
      void (async () => {
        for (const line of CREDITS) {
          if (!alive) return;
          await ctx.overlay.showCenter(line, 2);
        }
        if (!alive) return;
        await ctx.overlay.showCenter(TITLE_CARD, 2.5);
      })();
    },
    onUpdate(_ctx, rt) {
      if (introEndsAt !== null && rt.time >= introEndsAt) {
        introEndsAt = null;
        rt.say([FIRST_LINE]);
      }
      if (smokeEndsAt !== null && rt.time >= smokeEndsAt) {
        smokeEndsAt = null;
        rt.say([AFTER_SMOKE_LINE]);
      }
    },
    onExamine(ctx, rt, item) {
      if (item.id !== 'cigarette') return;
      rt.freeze(SMOKE_SECONDS);
      smokeEndsAt = rt.time + SMOKE_SECONDS;
      void ctx.overlay.showSmoke(SMOKE_SECONDS);
    },
    onExit(ctx) {
      alive = false;
      introEndsAt = null;
      smokeEndsAt = null;
      ctx.overlay.cancelCenter();
    },
  };
}
