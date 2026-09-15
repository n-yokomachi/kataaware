import type { Hooks } from '../runtime';

export const CREDITS = ['（仮）クレジット 1', '（仮）クレジット 2'];
export const TITLE_CARD = '（仮）タイトル';

/** 眩暈が消えるまでの間にクレジットとタイトルを順に出し、消えたら最初の独白を流す。場面が終わっていたら途中でやめる */
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
        rt.say(['（仮）他人の記憶を観た後は、いつもこうなる。']);
      })();
    },
    onExit(ctx) {
      alive = false;
      ctx.overlay.cancelCenter();
    },
  };
}
