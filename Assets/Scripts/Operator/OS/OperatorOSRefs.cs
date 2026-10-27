using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The data OperatorOS serializes: its fixed labels, the terminal's hidden commands and one reference class per part of
// the screen (filled by Last Terminal/16). Declarations only; the behavior is in OperatorOS.cs and the Os* parts.
public partial class OperatorOS
{
    // a fixed text of the UI in both languages (icon names, menu entries, buttons)
    [Serializable]
    public class LabelRef
    {
        // The text and what it says in each language.
        public TMP_Text text;
        public string en;
        public string pt;
    }

    // a hidden command of the terminal ("cmd" from Start > Run). Several names: "door|porta".
    [Serializable]
    public class TerminalEntry
    {
        // What is typed (names split by '|'), the answer per language.
        public string command = "";
        [TextArea(2, 10)] public string responseEn = "";
        [TextArea(2, 10)] public string responsePt = "";
        [Tooltip("Optional StoryProgress flag set when typed.")]
        public string setFlag = "";
    }

    // The full-screen stages: off, BIOS text, logo, login, desktop.
    [Serializable]
    public class ScreenRefs
    {
        // One CanvasGroup per stage (and the BIOS text).
        public CanvasGroup off;
        public CanvasGroup bios;
        public TMP_Text biosText;
        public CanvasGroup logo;
        public CanvasGroup loginScreen;
        public CanvasGroup desktopScreen;
    }

    // The login screen.
    [Serializable]
    public class LoginRefs
    {
        // User name text, password field, OK / Stand up buttons and the error message.
        public TMP_Text user;
        public TMP_InputField passwordInput;
        public Button ok;
        public Button stand;
        public TMP_Text message;
    }

    // The desktop: icons, the Start menu and its entries, clock and status line.
    [Serializable]
    public class DesktopRefs
    {
        // Click on the background (closes the Start menu) and the desktop icons.
        public Button background;
        public Button cameras;
        public Button computer;
        public Button mail;
        // The e-mail icon label (shows the unread count).
        public TMP_Text mailLabel;
        public Button recycle;
        public Button settings;
        // The Start button, its menu and the menu entries.
        public Button start;
        public CanvasGroup startMenu;
        public Button startCameras;
        public Button startFiles;
        public Button startMail;
        public Button startRecycle;
        public Button startRun;
        public Button startSettings;
        public Button startStand;
        public Button startShutdown;
        // Taskbar clock and status line.
        public TMP_Text clock;
        public TMP_Text status;
    }

    // The file explorer window.
    [Serializable]
    public class ExplorerRefs
    {
        // The window, the folder list, the file list, the row template, the path text and the "empty" text.
        public OsWindow window;
        public RectTransform folders;
        public RectTransform list;
        public OperatorFileRow rowTemplate;
        public TMP_Text path;
        public TMP_Text empty;
    }

    // The text and picture viewers.
    [Serializable]
    public class ViewerRefs
    {
        // Text viewer window, its body and scroll.
        public OsWindow text;
        public TMP_Text textBody;
        public ScrollRect textScroll;
        // Picture viewer window, the picture, its aspect fitter and caption.
        public OsWindow image;
        public RawImage picture;
        public AspectRatioFitter pictureFitter;
        public TMP_Text caption;
    }

    // The e-mail window.
    [Serializable]
    public class MailRefs
    {
        // The window, the inbox list, the open mail's header / body / scroll, the attachment button and label.
        public OsWindow window;
        public RectTransform list;
        public TMP_Text header;
        public TMP_Text body;
        public ScrollRect bodyScroll;
        public Button attachment;
        public TMP_Text attachmentLabel;
        // The voice bar of an e-mail and its play / pause button and icons.
        [Header("Voice player")]
        public GameObject voiceBar;
        public Button voicePlay;
        public RawImage voiceIcon;
        public Texture2D playTexture;
        public Texture2D pauseTexture;
        [Tooltip("Stretched from the left: its right edge = how much was played.")]
        public RectTransform voiceFill;
        // Its label, the time text and the source that plays it.
        public TMP_Text voiceLabel;
        public TMP_Text voiceTime;
        public AudioSource voiceSource;
    }

    // The hidden terminal: window, output text, scroll and input line.
    [Serializable]
    public class TerminalRefs
    {
        // Window, output, scroll and input.
        public OsWindow window;
        public TMP_Text output;
        public ScrollRect scroll;
        public TMP_InputField input;
    }

    // The Run dialog and the message box.
    [Serializable]
    public class DialogRefs
    {
        // Run: window, input, OK / Cancel. Message box: window, text, OK.
        public OsWindow run;
        public TMP_InputField runInput;
        public Button runOk;
        public Button runCancel;
        public OsWindow message;
        public TMP_Text messageText;
        public Button messageOk;
    }
}
