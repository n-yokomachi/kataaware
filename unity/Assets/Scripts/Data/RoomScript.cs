using UnityEngine;

namespace HalfAware
{
    /// <summary>場面 1 つ分の文面。シナリオの文をコードとシーンから分けて持つ</summary>
    [CreateAssetMenu(fileName = "RoomScript", menuName = "HalfAware/Room Script")]
    public sealed class RoomScript : ScriptableObject
    {
        [SerializeField] ScriptEntry[] entries = new ScriptEntry[0];

        /// <summary>id で引く。見つからなければ id が null の空の項目を返す</summary>
        public ScriptEntry Find(string id)
        {
            return ScriptEntry.Find(entries, id);
        }

        /// <summary>この文面が持つ id の一覧。シーンとの食い違いを調べるのに使う</summary>
        public string[] Ids()
        {
            var ids = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++) ids[i] = entries[i].id;
            return ids;
        }
    }
}
