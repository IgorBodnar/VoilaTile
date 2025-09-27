namespace VoilaTile.Snapper.EventArgs
{
    using System;
    using VoilaTile.Snapper.Input;

    public class HotKeyEventArgs : EventArgs
    {
        public HotKeyEventArgs(InputFeature inputFeature)
        {
            this.InputFeature = inputFeature; 
        }

        public InputFeature InputFeature { get; private set; }
    }
}
