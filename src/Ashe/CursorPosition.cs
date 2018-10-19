using System.Drawing;

namespace WagahighChoices.Ashe
{
    internal static class CursorPosition
    {
        /// <summary>
        /// 何もない安全地帯
        /// </summary>
        public static PointF Neutral { get; } = new PointF(0.01f, 0.01f);

        /// <summary>
        /// はじめから
        /// </summary>
        public static PointF NewGame { get; } = new PointF(0.15f, 0.95f);

        /// <summary>
        /// 選択肢 上
        /// </summary>
        public static PointF UpperChoice { get; } = new PointF(0.50f, 0.30f);

        /// <summary>
        /// 選択肢 下
        /// </summary>
        public static PointF LowerChoice { get; } = new PointF(0.50f, 0.42f);

        /// <summary>
        /// クイックセーブ
        /// </summary>
        public static PointF QuickSave { get; } = new PointF(0.57f, 0.98f);

        /// <summary>
        /// クイックロード
        /// </summary>
        public static PointF QuickLoad { get; } = new PointF(0.63f, 0.98f);

        /// <summary>
        /// YES/NO 選択肢の YES
        /// </summary>
        public static PointF Yes { get; } = new PointF(0.42f, 0.50f);

        /// <summary>
        /// 次の選択肢に進む
        /// </summary>
        public static PointF GoToNextSelection { get; } = new PointF(0.87f, 0.98f);
    }
}
