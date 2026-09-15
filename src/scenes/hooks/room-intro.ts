import type { Hooks } from '../runtime';

export const CREDITS = ['制作 〔名義〕'];
export const TITLE_CARD = 'HALF AWARE';
export const FIRST_LINE = 'うぅ…今回は酔いが酷い…';
export const AFTER_SMOKE_LINE = '煙草が切れた…買いに行くついでに今日のメモリも売っちゃおう';
/** 煙草を取ってから吸い終わるまでの秒数 */
export const SMOKE_SECONDS = 4;

/**
 * 眩暈の中でクレジットとタイトルを順に出し、消えたら最初の独白を流す。
 * 煙草を取ったら、煙を立てて数秒止め、吸い終わりの独白を流す。
 * 場面が終わっていたら途中でやめる。
 */
export function roomIntroHooks(): Hooks {
  let alive = false;
  return {
    onEnter(ctx, rt) {
      alive = true;
      void (async () => {
        for (const line of CREDITS) {
          if (!alive) return;
          await ctx.overlay.showCenter(line, 2);
        }
        if (!alive) return;
        await ctx.overlay.showCenter(TITLE_CARD, 2.5);
        if (!alive) return;
        rt.say([FIRST_LINE]);
      })();
    },
    onExamine(ctx, rt, item) {
      if (item.id !== 'cigarette') return;
      rt.freeze(SMOKE_SECONDS);
      void (async () => {
        await ctx.overlay.showSmoke(SMOKE_SECONDS);
        if (alive) rt.say([AFTER_SMOKE_LINE]);
      })();
    },
    onExit(ctx) {
      alive = false;
      ctx.overlay.cancelCenter();
    },
  };
}
