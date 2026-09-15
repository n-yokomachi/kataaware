import type { Hooks } from '../runtime';

export const CREDITS = ['（仮）クレジット 1', '（仮）クレジット 2'];
export const TITLE_CARD = '（仮）タイトル';

/** 眩暈が消えるまでの間にクレジットとタイトルを順に出し、消えたら最初の独白を流す */
export function roomIntroHooks(): Hooks {
  return {
    onEnter(ctx, rt) {
      void (async () => {
        for (const line of CREDITS) await ctx.overlay.showCenter(line, 2);
        await ctx.overlay.showCenter(TITLE_CARD, 2.5);
        rt.say(['（仮）他人の記憶を観た後は、いつもこうなる。']);
      })();
    },
  };
}
