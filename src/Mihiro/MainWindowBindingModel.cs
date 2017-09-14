namespace WagahighChoices.Mihiro
{
    public class MainWindowBindingModel : BindingModelBase
    {
        private int _gameWidth;
        public int GameWidth
        {
            get => this._gameWidth;
            set => this.SetToBackingField(ref this._gameWidth, value);
        }

        private int _gameHeight;
        public int GameHeight
        {
            get => this._gameHeight;
            set => this.SetToBackingField(ref this._gameHeight, value);
        }

        private int _cursorX;
        public int CursorX
        {
            get => this._cursorX;
            set => this.SetToBackingField(ref this._cursorX, value);
        }

        private int _cursorY;
        public int CursorY
        {
            get => this._cursorY;
            set => this.SetToBackingField(ref this._cursorY, value);
        }

        private double _cursorRatioX;
        public double CursorRatioX
        {
            get => this._cursorRatioX;
            set => this.SetToBackingField(ref this._cursorRatioX, value);
        }

        private double _cursorRatioY;
        public double CursorRatioY
        {
            get => this._cursorRatioY;
            set => this.SetToBackingField(ref this._cursorRatioY, value);
        }
    }
}
