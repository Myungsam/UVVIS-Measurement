using System.Drawing;
using System.Windows.Forms;

namespace UvVisMeasurement.Ui
{
    /// <summary>
    /// Every colour and control style of the application, in one place so a brand palette
    /// can be dropped in by editing this file alone.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(0x1F, 0x5F, 0xAD);
        public static readonly Color AccentHover = Color.FromArgb(0x2A, 0x72, 0xC8);
        public static readonly Color AccentSoft = Color.FromArgb(0xE6, 0xEF, 0xFA);

        public static readonly Color Success = Color.FromArgb(0x2E, 0x8B, 0x57);
        public static readonly Color SuccessSoft = Color.FromArgb(0xE3, 0xF3, 0xEA);
        public static readonly Color Warning = Color.FromArgb(0xC7, 0x77, 0x00);
        public static readonly Color Danger = Color.FromArgb(0xC0, 0x39, 0x2B);

        public static readonly Color Surface = Color.FromArgb(0xF3, 0xF5, 0xF8);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(0xCF, 0xD6, 0xDF);
        public static readonly Color Text = Color.FromArgb(0x1E, 0x24, 0x2B);
        public static readonly Color TextMuted = Color.FromArgb(0x5F, 0x6B, 0x7A);

        public static void StyleButton(ButtonBase button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = Card;
            button.ForeColor = Text;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = AccentSoft;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xD4, 0xE3, 0xF6);
        }

        public static void StylePrimary(ButtonBase button)
        {
            StyleButton(button);
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = Accent;
        }

        public static void StyleGroup(GroupBox group)
        {
            group.ForeColor = Accent;
            foreach (Control child in group.Controls)
            {
                if (!(child is ButtonBase) && child.ForeColor == Accent)
                    child.ForeColor = Text;
            }
        }

        public sealed class ToolColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Card; } }
            public override Color ToolStripGradientMiddle { get { return Card; } }
            public override Color ToolStripGradientEnd { get { return Card; } }
            public override Color ToolStripBorder { get { return Border; } }
            public override Color MenuStripGradientBegin { get { return Card; } }
            public override Color MenuStripGradientEnd { get { return Card; } }
            public override Color StatusStripGradientBegin { get { return Surface; } }
            public override Color StatusStripGradientEnd { get { return Surface; } }
            public override Color ButtonSelectedHighlight { get { return AccentSoft; } }
            public override Color ButtonSelectedGradientBegin { get { return AccentSoft; } }
            public override Color ButtonSelectedGradientMiddle { get { return AccentSoft; } }
            public override Color ButtonSelectedGradientEnd { get { return AccentSoft; } }
            public override Color ButtonSelectedBorder { get { return Accent; } }
            public override Color ButtonCheckedGradientBegin { get { return SuccessSoft; } }
            public override Color ButtonCheckedGradientMiddle { get { return SuccessSoft; } }
            public override Color ButtonCheckedGradientEnd { get { return SuccessSoft; } }
            public override Color ButtonCheckedHighlight { get { return SuccessSoft; } }
            public override Color ButtonPressedGradientBegin { get { return AccentSoft; } }
            public override Color ButtonPressedGradientMiddle { get { return AccentSoft; } }
            public override Color ButtonPressedGradientEnd { get { return AccentSoft; } }
            public override Color MenuItemSelected { get { return AccentSoft; } }
            public override Color MenuItemBorder { get { return Accent; } }
            public override Color MenuItemSelectedGradientBegin { get { return AccentSoft; } }
            public override Color MenuItemSelectedGradientEnd { get { return AccentSoft; } }
            public override Color MenuItemPressedGradientBegin { get { return Surface; } }
            public override Color MenuItemPressedGradientEnd { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Card; } }
        }

        public static ToolStripRenderer CreateRenderer()
        {
            return new ToolStripProfessionalRenderer(new ToolColors()) { RoundedEdges = false };
        }
    }
}
