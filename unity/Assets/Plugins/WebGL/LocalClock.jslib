// ブラウザの時差（分）。セーブの行に出す「書いた日時」をその土地の時刻にするため（LocalClock.cs）
mergeInto(LibraryManager.library, {
  HalfAwareTimezoneOffset: function () {
    return new Date().getTimezoneOffset();
  }
});
