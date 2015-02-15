using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZkData;

namespace ZeroKLobby.MicroLobby
{
    public partial class BattleListTab: UserControl, INavigatable
    {
        BattleListControl battleListControl;

        public BattleListTab() 
        {
            Paint += BattleListTab_Enter;
            BackColor = Color.White;
            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        void BattleListTab_Enter(object sender, EventArgs e) //lazy initialization
        {
            Paint -= BattleListTab_Enter; //using "Paint" instead of "Enter" event because "Enter" is too lazy in Mono (have to click control)
            SuspendLayout(); //pause
            InitializeComponent();

            if (DesignMode) return;
            DpiMeasurement.DpiXYMeasurement(this);
            var lookingGlass = new PictureBox
            {
                Width =  DpiMeasurement.ScaleValueY(20),
                Height = DpiMeasurement.ScaleValueY(20),
                Image = ZklResources.search,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Left
            };
            Program.ToolTip.SetText(lookingGlass, "Search game, description, map or player");
            Program.ToolTip.SetText(searchBox, "Search game, description, map or player");
            
            hideEmptyBox.Checked = Program.Conf.HideEmptyBattles;
            hideFullBox.Checked = Program.Conf.HideNonJoinableBattles;
            showOfficialBox.Checked = Program.Conf.ShowOfficialBattles;
            hidePasswordedBox.Checked = Program.Conf.HidePasswordedBattles;

            // battle list
            battleListControl = new BattleListControl() { Dock = DockStyle.Fill };
            battlePanel.Controls.Add(battleListControl);
            ResumeLayout();
        }

        public bool TryNavigate(params string[] path) {
            if (path.Length == 0) return false;
            if (path[0] != PathHead) return false;

            if (path.Length == 2 && !String.IsNullOrEmpty(path[1])) {
                var gameShortcut = path[1];
                if (battleListControl == null) Program.Conf.BattleFilter = gameShortcut;
                else battleListControl.FilterText = gameShortcut;
            }
            else {
                if (battleListControl == null) Program.Conf.BattleFilter = "";
                else battleListControl.FilterText = "";
            }
            return true;
        }


        public string PathHead { get { return "battles"; } }

        public bool Hilite(HiliteLevel level, string path) {
            return false;
        }


        private void PaintParentBackground(Control par, PaintEventArgs e)
        {
            if (par != null)
            {
                Point loc = par.PointToClient(Parent.PointToScreen(Location));


                Rectangle rect = new Rectangle(loc.X, loc.Y,
                                               Width, Height);

                e.Graphics.TranslateTransform(-rect.X, -rect.Y);

                try
                {
                    using (PaintEventArgs pea =
                                new PaintEventArgs(e.Graphics, rect))
                    {
                        pea.Graphics.SetClip(rect);
                        InvokePaintBackground(par, pea);
                        //InvokePaint(par, pea);
                    }
                }
                finally
                {
                    e.Graphics.TranslateTransform(rect.X, rect.Y);
                }
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.Control,
                                         ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //PaintParentBackground(Program.MainWindow, e);
            
            //ClientRectangle = new Rectangle(DisplayRectangle.Left+20, DisplayRectangle.Top+20, DisplayRectangle.Width-40,DisplayRectangle.Height-40);
            //ButtonRenderer.Instance.RenderToGraphics(e.Graphics,DisplayRectangle, ButtonRenderer.StyleType.Shraka );

            base.OnPaint(e);
        }



        void searchBox_TextChanged(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(searchBox.Text)) Program.MainWindow.navigationControl.Path = "battles/" + searchBox.Text;
            else Program.MainWindow.navigationControl.Path = "battles";
            battleListControl.FilterText = searchBox.Text;
        }

        void showEmptyBox_CheckedChanged(object sender, EventArgs e) {
            if (battleListControl != null) battleListControl.HideEmpty = hideEmptyBox.Checked;
        }

        void showFullBox_CheckedChanged(object sender, EventArgs e) {
            if (battleListControl != null) battleListControl.HideFull = hideFullBox.Checked;
        }

        void showOfficialButton_CheckedChanged(object sender, EventArgs e) {
            if (battleListControl != null) battleListControl.ShowOfficial = showOfficialBox.Checked;
        }

        private void hidePasswordedBox_CheckedChanged(object sender, EventArgs e)
        {
            if (battleListControl != null) battleListControl.HidePassworded = hidePasswordedBox.Checked;
        }

    }
}