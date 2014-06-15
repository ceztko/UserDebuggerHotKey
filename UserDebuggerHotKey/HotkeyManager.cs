using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace UserDebuggerHotKey
{
    // http://stackoverflow.com/questions/3654787/global-hotkey-in-console-application, Joe
    public static class HotKeyManager
    {
        [DllImport("user32", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        delegate bool RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key);
        delegate bool UnRegisterHotKeyDelegate(IntPtr hwnd, int id);

        public static event EventHandler<HotKeyEventArgs> HotKeyPressed;

        private static volatile MessageWindow _wnd;
        private static volatile IntPtr _hwnd;
        private static ManualResetEvent _windowReadyEvent;
        private static bool _useOwnMessageLoop;

        private static int _id = 0;

        static HotKeyManager()
        {
            if (Application.MessageLoop)
            {
                new MessageWindow();
                _windowReadyEvent = null;
                _useOwnMessageLoop = false;
            }
            else
            {
                _windowReadyEvent = new ManualResetEvent(false);

                Thread messageLoop = new Thread(delegate()
                {
                    Application.Run(new MessageWindow());
                });
                messageLoop.Name = "MessageLoopThread";
                messageLoop.IsBackground = true;
                messageLoop.Start();

                _useOwnMessageLoop = true;
            }
        }

        public static int RegisterHotKey(Keys key, KeyModifiers modifiers)
        {
            if (_useOwnMessageLoop)
                _windowReadyEvent.WaitOne();
            int id = Interlocked.Increment(ref _id);
            bool ok = (bool)_wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);
            if (ok)
                return id;
            else
                return -1;
        }

        public static bool UnregisterHotKey(int id)
        {
            bool ok = (bool)_wnd.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, id);
            return ok;
        }

        private static bool RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)
        {
            return RegisterHotKey(hwnd, id, modifiers, key);
        }

        private static bool UnRegisterHotKeyInternal(IntPtr hwnd, int id)
        {
            return UnregisterHotKey(_hwnd, id);
        }

        private static void OnHotKeyPressed(HotKeyEventArgs e)
        {
            EventHandler<HotKeyEventArgs> handler = HotKeyPressed;
            if (handler != null)
                handler(null, e);
        }

        public static bool UseOwnMessageLoop
        {
            get { return _useOwnMessageLoop; }
        }

        private class MessageWindow : Form
        {
            public MessageWindow()
            {
                _wnd = this;
                _hwnd = this.Handle;
                if (UseOwnMessageLoop)
                    _windowReadyEvent.Set();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotKeyEventArgs e = new HotKeyEventArgs(m.LParam);
                    HotKeyManager.OnHotKeyPressed(e);
                }

                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                // Ensure the window never becomes visible
                base.SetVisibleCore(false);
            }

            private const int WM_HOTKEY = 0x312;
        }
    }


    public class HotKeyEventArgs : EventArgs
    {
        public readonly Keys Key;
        public readonly KeyModifiers Modifiers;

        public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
        {
            this.Key = key;
            this.Modifiers = modifiers;
        }

        public HotKeyEventArgs(IntPtr hotKeyParam)
        {
            uint param = (uint)hotKeyParam.ToInt64();
            Key = (Keys)((param & 0xffff0000) >> 16);
            Modifiers = (KeyModifiers)(param & 0x0000ffff);
        }
    }

    [Flags]
    public enum KeyModifiers
    {
        None =  0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }
}
