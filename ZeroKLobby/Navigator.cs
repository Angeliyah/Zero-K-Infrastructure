using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ZeroKLobby.MapDownloader;
using ZeroKLobby.MicroLobby;

namespace ZeroKLobby
{
    public class Navigator
    {
        NavigationStep _currentPage;
        readonly Stack<NavigationStep> backStack = new Stack<NavigationStep>();
        readonly ChatTab chatTab;
        readonly Stack<NavigationStep> forwardStack = new Stack<NavigationStep>();
        readonly Dictionary<INavigatable, string> lastTabPaths = new Dictionary<INavigatable, string>();
        public ChatTab ChatTab
        {
            get { return chatTab; }
        }


        static List<ButtonInfo> ButtonList { get; set; }
        bool CanGoBack
        {
            get { return backStack.Any(); }
        }
        bool CanGoForward
        {
            get { return forwardStack.Any(); }
        }

        NavigationStep CurrentPage
        {
            get { return _currentPage; }
            set
            {
                _currentPage = value;

                ButtonList.ForEach(x => x.IsSelected = false); //unselect all button

                var selbut = ButtonList.Where(x => Path.StartsWith(x.TargetPath)).OrderByDescending(x => x.TargetPath.Length).FirstOrDefault();
                if (selbut != null)
                {
                    selbut.IsSelected = true;
                    selbut.IsAlerting = false;
                }


                var navigable =
                    tabs.Controls.OfType<Object>().Select(GetINavigatableFromControl).FirstOrDefault(x => x != null && Path.StartsWith(x.PathHead));
                //find TAB with correct PathHead
                if (navigable != null) navigable.Hilite(HiliteLevel.None, Path); //cancel hilite ChatTab's tab (if meet some condition)
            }
        }

        public string Path
        {
            get { return CurrentPage != null ? CurrentPage.ToString() : string.Empty; }
            set
            {
                if (value.ToLower().StartsWith("zk://")) value = value.Substring(5);

                var parts = value.Split('@');
                for (var i = 1; i < parts.Length; i++)
                {
                    var action = parts[i];
                    ActionHandler.PerformAction(action);
                }
                value = parts[0];

                if (CurrentPage != null && CurrentPage.ToString() == value) return; // we are already there, no navigation needed

                if (value.StartsWith("www."))
                {
                    value = "http://" + value;
                } //create "http://www"
                var step = GoToPage(value.Split('/')); //go to page
                if (step != null)
                {
                    if (CurrentPage != null && CurrentPage.ToString() != value) backStack.Push(CurrentPage);
                    CurrentPage = step;
                }
                else if (value.StartsWith("http://") || value.StartsWith("https://") || value.StartsWith("file://"))
                {
                    Program.BrowserInterop.OpenUrl(value); //this open external browser
                }
            }
        }

        Control buttonPanel;
        TabControl tabs;

        public Navigator(TabControl tabs, Control buttonPanel)
        {
            this.buttonPanel = buttonPanel;
            this.tabs = tabs;

            //(Increase performance), Reference: http://msdn.microsoft.com/en-us/library/system.windows.forms.control.suspendlayout.aspx

            ButtonList = new List<ButtonInfo>() //normal arrangement
            {
                new ButtonInfo() { Label = "Chat", TargetPath = "chat", Icon = ZklResources.chat, Height = 32, Width = 65 },
                new ButtonInfo() { Label = "Quick browse", TargetPath = "battles", Icon = ZklResources.battle, Width = 115, Height = 32, },
                new ButtonInfo() { Label = "Extras", TargetPath = "extras", Height = 32, },
                new ButtonInfo() {
                    Label = "Settings",
                    TargetPath = "settings",
                    Icon = Buttons.settings,
                    Height = 32,
                    Width = 100,
                    Dock = DockStyle.Right
                },
            };

            foreach (var b in ButtonList) buttonPanel.Controls.Add(b.GetButton());

            tabs.TabPages.Clear();

            chatTab = new ChatTab();

            lastTabPaths[chatTab] = string.Format("chat/channel/{0}",
                Program.Conf != null ? Program.Conf.AutoJoinChannels.OfType<string>().FirstOrDefault() : "zk");
            AddTabPage(chatTab, "Chat");

            var battles = new BattleListTab();
            AddTabPage(battles, "Battles");
            AddTabPage(new SettingsTab(), "Settings");
            AddTabPage(new ServerTab(), "Server");
            AddTabPage(new DownloaderTab(), "Rapid");
        }



        public bool HilitePath(string navigationPath, HiliteLevel hiliteLevel)
        {
            if (string.IsNullOrEmpty(navigationPath)) return false;
            if (hiliteLevel == HiliteLevel.Flash) foreach (var b in ButtonList) if (navigationPath.StartsWith(b.TargetPath)) b.IsAlerting = true; //make BUTTON turn red

            var navigable = tabs.Controls.OfType<Object>().Select(GetINavigatableFromControl).First(x => x != null && navigationPath.Contains(x.PathHead));
            if (navigable != null) return navigable.Hilite(hiliteLevel, navigationPath); //make ChatTab's tab to flash
            else return false;
        }

        public void NavigateBack()
        {
            if (CanGoBack) GoBack();
        }

        public void NavigateForward()
        {
            if (CanGoForward) GoForward();
        }

        public INavigatable GetNavigatableFromPath(string path)
        {
            return tabs.Controls.OfType<Control>().Select(GetINavigatableFromControl).FirstOrDefault(x => path.StartsWith(x.PathHead));
        }

        public void SwitchTab(string targetPath)
        { //called by ButtonInfo.cs when clicked. "targetPath" is usually a "PathHead"
            foreach (TabPage tabPage in tabs.Controls) {
                var nav = GetINavigatableFromControl(tabPage);
                if (nav.PathHead == targetPath) {
                    if (CurrentNavigatable == nav) {
                        Path = targetPath; // double click on forum go to forum home
                    } else {
                        string lastPath;
                        if (lastTabPaths.TryGetValue(nav, out lastPath)) targetPath = lastPath; //go to current page of the tab
                        Path = targetPath;
                    }
                    return;
                }
            }
            Path = targetPath;
        }


        TabPage AddTabPage(Control content, string name = null)
        {
            name = name ?? content.Text ?? content.Name;
            var tb = new TabPage(name);
            tb.Dock = DockStyle.Fill;
            tb.Controls.Add(content);
            content.Dock = DockStyle.Fill;
            tabs.TabPages.Add(tb);
            return tb;
        }

        INavigatable GetINavigatableFromControl(object obj)
        {
            if (obj is TabPage) obj = ((TabPage)obj).Controls.OfType<Control>().FirstOrDefault();
            return obj as INavigatable;
        }


        void GoBack()
        {
            if (forwardStack.Count == 0 || forwardStack.Peek().ToString() != CurrentPage.ToString()) forwardStack.Push(CurrentPage);
            CurrentPage = backStack.Pop();
            GoToPage(CurrentPage.Path);
        }

        void GoForward()
        {
            if (backStack.Count == 0 || backStack.Peek().ToString() != CurrentPage.ToString()) backStack.Push(CurrentPage);
            CurrentPage = forwardStack.Pop();
            GoToPage(CurrentPage.Path);
        }

        void SetHeader(string text)
        {
            Program.MainWindow.lbRightPanelTitle.Text = text;
        }

        NavigationStep GoToPage(string[] path) // todo cleanup
        {
            foreach (TabPage tabPage in tabs.Controls) {
                var navigatable = GetINavigatableFromControl(tabPage); //translate tab button into the page it represent
                if (navigatable != null && navigatable.TryNavigate(path)) {
                    tabs.SelectTab(tabPage);
                    lastTabPaths[navigatable] = string.Join("/", path);
                    SetHeader(navigatable.Title);

                    return new NavigationStep { Path = path };
                }
            }
            return null;
        }



        class NavigationStep
        {
            public string[] Path { get; set; }

            public override string ToString()
            {
                return string.Join("/", Path);
            }
        }


        public INavigatable CurrentNavigatable
        {
            get { return tabs.SelectedTab.Controls.OfType<INavigatable>().FirstOrDefault(); }
        }
    }
}