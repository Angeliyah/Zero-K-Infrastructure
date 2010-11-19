using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using JetBrains.Annotations;
using LobbyClient;
using PlasmaShared;

namespace ZeroKLobby.MicroLobby
{
  public partial class BattleListControl: ScrollableControl
  {
    readonly Dictionary<BattleIcon, Point> battleIconPositions = new Dictionary<BattleIcon, Point>();
    readonly TextBox filterBox;
    Battle hoverBattle;
    readonly IEnumerable<BattleIcon> model;
    Point previousLocation;
    bool showEmpty;
    bool showNonJoinable = true;
    bool sortByPlayers;

    List<BattleIcon> view = new List<BattleIcon>();

    public string FilterText
    {
      get { return filterBox.Text; }
      set
      {
        filterBox.Text = value;
        Program.Conf.BattleFilter = filterBox.Text;
        Program.SaveConfig();
        FilterBattles();
        Sort();
        Invalidate();
      }
    }

    public BattleListControl(TextBox filterBox)
    {
      this.filterBox = filterBox;
      InitializeComponent();
      AutoScroll = true;
      SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
      BackColor = Color.White;
      filterBox.Text = Program.Conf.BattleFilter;
      filterBox.TextChanged += box_TextChanged;
      Disposed += BattleListControl_Disposed;
      Program.BattleIconManager.BattleAdded += HandleBattle;
      Program.BattleIconManager.BattleChanged += HandleBattle;
      Program.BattleIconManager.RemovedBattle += HandleBattle;
      model = Program.BattleIconManager.BattleIcons;
      sortByPlayers = Program.Conf.SortBattlesByPlayers;
      showEmpty = Program.Conf.ShowEmptyBattles;
      showNonJoinable = Program.Conf.ShowNonJoinableBattles;

      FilterBattles();
      Invalidate();
    }

    void BattleListControl_Disposed(object sender, EventArgs e)
    {
      filterBox.TextChanged -= box_TextChanged;
      Program.BattleIconManager.BattleChanged -= HandleBattle;
      Program.BattleIconManager.BattleAdded -= HandleBattle;
      Program.BattleIconManager.RemovedBattle -= HandleBattle;
    }

    public static IEnumerable<Battle> BattleWordFilter(IEnumerable<Battle> battles, string filter)
    {
      if (string.IsNullOrEmpty(filter)) return battles;
      var words = filter.ToUpper().Split(' ');
      return battles.Where(x => BattleWordFilter(x, words));
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
    public ContextMenu GetContextMenu()
    {
      var menu = new ContextMenu();

      var showNonJoinableItem = new MenuItem("Show Locked, Passworded, Running and Full Battles") { Checked = showNonJoinable };
      showNonJoinableItem.Click += (s, ea) =>
        {
          showNonJoinable = !showNonJoinable;
          Program.Conf.ShowNonJoinableBattles = showNonJoinable;
          FilterBattles();
          Sort();
          Invalidate();
        };
      menu.MenuItems.Add(showNonJoinableItem);

      var showEmptyItem = new MenuItem("Show Empty Battles") { Checked = showEmpty };
      showEmptyItem.Click += (s, ea) =>
        {
          showEmpty = !showEmpty;
          Program.Conf.ShowEmptyBattles = showEmpty;
          FilterBattles();
          Sort();
          Invalidate();
        };
      menu.MenuItems.Add(showEmptyItem);
      var sortItem = new MenuItem("Sort by Players") { Checked = sortByPlayers };
      sortItem.Click += (s, ea) =>
        {
          sortByPlayers = !sortByPlayers;
          Program.Conf.SortBattlesByPlayers = sortByPlayers;
          Sort();
          Invalidate();
        };
      menu.MenuItems.Add(sortItem);
      return menu;
    }

    protected override void OnMouseDown([NotNull] MouseEventArgs e)
    {
      if (e == null) throw new ArgumentNullException("e");
      base.OnMouseDown(e);
      if (e.Button == MouseButtons.Left)
      {
        var battle = GetBattle(e.X, e.Y);
        if (battle != null)
        {
          if (battle.Password != "*")
          {
            // hack dialog Program.FormMain
            using (var form = new AskBattlePasswordForm(battle.Founder)) if (form.ShowDialog() == DialogResult.OK) ActionHandler.JoinBattle(battle.BattleID, form.Password);
          }
          else ActionHandler.JoinBattle(battle.BattleID, null);
        }
        else if (OpenGameButtonHitTest(e.X, e.Y)) ShowHostDialog(KnownGames.GetDefaultGame());
      }
      else if (e.Button == MouseButtons.Right)
      {
        var menu = GetContextMenu();
        menu.Show(this, e.Location);
      }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
      base.OnMouseMove(e);
      var battle = GetBattle(e.X, e.Y);
      var openGameButtonHit = OpenGameButtonHitTest(e.X, e.Y);
      Cursor = battle != null || openGameButtonHit ? Cursors.Hand : Cursors.Default;
      var cursorPoint = new Point(e.X, e.Y);
      if (cursorPoint == previousLocation) return;
      previousLocation = cursorPoint;

      UpdateTooltip(battle);
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
      try
      {
        base.OnPaint(pe);
        var g = pe.Graphics;
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        battleIconPositions.Clear();
        var x = 0;
        var y = 0;

        g.DrawImage(Resources.Border, 3, 3, 70, 70);
        g.DrawString("Open a new battle.", BattleIcon.TitleFont, BattleIcon.TextBrush, BattleIcon.MapCellSize.Width, y + 3);

        x += BattleIcon.Width;

        foreach (var t in view)
        {
          if (x + BattleIcon.Width > Width)
          {
            x = 0;
            y += BattleIcon.Height;
          }
          battleIconPositions[t] = new Point(x, y);
          if (g.VisibleClipBounds.IntersectsWith(new RectangleF(x, y, BattleIcon.Width, BattleIcon.Height))) g.DrawImageUnscaled(t.Image, x, y);
          x += BattleIcon.Width;
        }

        if (view.Count < model.Count())
        {
          if (x + BattleIcon.Width > Width)
          {
            x = 0;
            y += BattleIcon.Height;
          }

          g.DrawString(
            filterBox.Text.Length == 0
              ? "Some Battles are hidden. Click on the More button to display them."
              : "Search results are being displayed. To reset the search, empty the search box below.",
            BattleIcon.TitleFont,
            BattleIcon.TextBrush,
            new Rectangle(x, y, BattleIcon.Width, BattleIcon.Height));
        }

        AutoScrollMinSize = new Size(0, y + BattleIcon.Height);
      }
      catch (Exception e)
      {
        Trace.WriteLine("Error in drawing battles: " + e);
      }
    }

    public static bool BattleWordFilter(Battle x, string[] words)
    {
      var hide = false;
      foreach (var wordIterated in words)
      {
        var word = wordIterated;
        var negation = false;
        if (word.StartsWith("-"))
        {
          word = word.Substring(1);
          negation = true;
        }
        if (String.IsNullOrEmpty(word)) continue; // dont filter empty words

        bool isSpecialWordMatch;
        if (FilterSpecialWordCheck(x, word, out isSpecialWordMatch)) // if word is mod shortcut, handle specially
        {
          if ((!negation && !isSpecialWordMatch) || (negation && isSpecialWordMatch))
          {
            hide = true;
            break;
          }
        }
        else
        {
          var playerFound = x.Users.Any(u => u.Name.ToUpper().Contains(word));
          var titleFound = x.Title.ToUpper().Contains(word);
          var modFound = x.ModName.ToUpper().Contains(word);
          var mapFound = x.MapName.ToUpper().Contains(word);
          if (!negation)
          {
            if (!(playerFound || titleFound || modFound || mapFound))
            {
              hide = true;
              break;
            }
          }
          else
          {
            if (playerFound || titleFound || modFound || mapFound) // for negation ignore players
            {
              hide = true;
              break;
            }
          }
        }
      }
      return (!hide);
    }


    void FilterBattles()
    {
      if (model == null) return;
      view.Clear();
      if (String.IsNullOrEmpty(Program.Conf.BattleFilter)) view = model.ToList();
      else
      {
        var words = Program.Conf.BattleFilter.ToUpper().Split(' ');
        view = model.Where(x => BattleWordFilter(x.Battle, words)).ToList();
      }
      IEnumerable<BattleIcon> v = view; // speedup to avoid multiple "toList"
      if (!showEmpty) v = v.Where(bi => bi.Battle.NonSpectatorCount > 0);
      if (!showNonJoinable)
      {
        v =
          v.Where(
            bi =>
            !bi.Battle.IsLocked && !bi.Battle.IsPassworded && !Program.TasClient.ExistingUsers[bi.Battle.Founder].IsInGame &&
            bi.Battle.NonSpectatorCount < bi.Battle.MaxPlayers);
      }

      view = v.ToList();
    }

    static bool FilterSpecialWordCheck(Battle battle, string word, out bool isMatch)
    {
      // mod shortcut 
      var knownGame = KnownGames.List.SingleOrDefault(x => x.Shortcut.ToUpper() == word);
      if (knownGame != null)
      {
        isMatch = battle.ModName != null && Regex.IsMatch(battle.ModName, knownGame.Regex);
        return true;
      }
      else
      {
        switch (word)
        {
          case "LOCK":
            isMatch = battle.IsLocked;
            return true;
          case "PASSWORD":
            isMatch = battle.Password != "*";
            return true;
          case "INGAME":
            isMatch = Program.TasClient.ExistingUsers[battle.Founder].IsInGame;
            return true;
          case "FULL":
            isMatch = battle.NonSpectatorCount >= battle.MaxPlayers;
            return true;
        }
      }

      isMatch = false;
      return false;
    }

    Battle GetBattle(int x, int y)
    {
      x -= AutoScrollPosition.X;
      y -= AutoScrollPosition.Y;
      foreach (var kvp in battleIconPositions)
      {
        var battleIcon = kvp.Key;
        var position = kvp.Value;
        var battleIconRect = new Rectangle(position.X, position.Y, BattleIcon.Width, BattleIcon.Height);
        if (battleIconRect.Contains(x, y) && battleIcon.HitTest(x - position.X, y - position.Y)) return battleIcon.Battle;
      }
      return null;
    }

    bool OpenGameButtonHitTest(int x, int y)
    {
      return x > 3 && x < 71 && y > 3 && y < 71;
    }

    void ShowHostDialog(GameInfo filter)
    {
      using (var dialog = new HostDialog(filter))
      {
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var springieCommands = dialog.SpringieCommands.Lines();

        ActionHandler.StopBattle();

        ActionHandler.SpawnAutohost(dialog.GameName,
                                    dialog.BattleTitle,
                                    dialog.Password,
                                    dialog.IsManageEnabled,
                                    dialog.MinPlayers,
                                    dialog.MaxPlayers,
                                    dialog.Teams,
                                    springieCommands);
      }
    }


    void Sort()
    {
      if (sortByPlayers) view = view.OrderByDescending(bi => bi.Battle.NonSpectatorCount).ToList();
    }

    void UpdateTooltip(Battle battle)
    {
      if (hoverBattle != battle)
      {
        hoverBattle = battle;
        if (battle != null) Program.ToolTip.SetBattle(this, battle.BattleID);
        else Program.ToolTip.SetText(this, null);
      }
    }

    void HandleBattle(object sender, EventArgs<BattleIcon> e)
    {
      var invalidate = view.Contains(e.Data);
      FilterBattles();
      Sort();
      var point = PointToClient(MousePosition);
      UpdateTooltip(GetBattle(point.X, point.Y));
      if (view.Contains(e.Data) || invalidate) Invalidate();
    }

    void box_TextChanged(object sender, EventArgs e)
    {
      if (!string.IsNullOrEmpty(filterBox.Text)) Program.MainWindow.navigationControl.Path = "battles/" + filterBox.Text;
      else Program.MainWindow.navigationControl.Path = "battles";
    }
  }
}