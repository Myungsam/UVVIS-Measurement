using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Ui
{
    /// <summary>Cell tints shared by the device table and the GPIO tables.</summary>
    internal static class GridColors
    {
        /// <summary>"This choice is not available right now."</summary>
        public static readonly Color Locked = Color.FromArgb(225, 225, 228);

        /// <summary>Cells that only ever display driver-supplied information.</summary>
        public static readonly Color ReadOnly = Color.FromArgb(246, 246, 248);
    }

    internal sealed class HardwareChangeRequestedEventArgs : EventArgs
    {
        public HardwareChangeRequestedEventArgs(HardwareChange change) { Change = change; }
        public HardwareChange Change { get; private set; }
    }

    /// <summary>
    /// The Hardware Settings tab: one section per connected spectrometer holding what the
    /// driver reports about it and the two settings that can be changed on the device -
    /// the measurement mode and the direction of each GPIO channel.
    ///
    /// Nothing here decides what a spectrometer can do. Every list is built from the
    /// <see cref="DeviceInfo"/> the session read from the driver, so a device with no GPIO
    /// gets no channels and a mode the driver refused is drawn but cannot be picked.
    ///
    /// The layout follows the device table on the parameter panel: a group box per topic,
    /// labels on the left, and a DataGridView for the per-channel rows.
    /// </summary>
    internal sealed class HardwareSettingsView
    {
        private const int GroupMargin = 8;
        private const int LabelLeft = 12;
        private const int ValueLeft = 160;
        private const int RowHeight = 22;

        // Columns of the GPIO table: Channel, Capability, Direction, Output, State.
        private const int DirectionColumn = 2;
        private const int OutputColumn = 3;
        private const int StateColumn = 4;

        private sealed class DeviceSection
        {
            public int DeviceIndex;

            /// <summary>
            /// Which physical spectrometer this section belongs to. Every request the
            /// section raises carries it, so a pin is only ever written on the device the
            /// operator was actually looking at - never on whoever holds that index later.
            /// </summary>
            public string Identity = string.Empty;

            public GroupBox Group;
            public Label Role;
            public ComboBox Mode;
            public Label ModeNote;
            public DataGridView Gpio;
            public Label GpioNote;
            public MeasurementModeOption[] Options = new MeasurementModeOption[0];
            public GpioChannelInfo[] Channels = new GpioChannelInfo[0];

            /// <summary>Mode currently shown, so a refused change can be put back.</summary>
            public TriggerMode Shown;
        }

        private readonly Panel _host;
        private readonly Label _empty;
        private readonly List<DeviceSection> _sections = new List<DeviceSection>();
        private bool _suppress;

        private DeviceInfo[] _devices = new DeviceInfo[0];
        private DeviceSettings[] _settings = new DeviceSettings[0];
        private bool _stale = true;

        public HardwareSettingsView(Panel host)
        {
            _host = host;
            _host.AutoScroll = true;
            _host.Resize += (s, e) => ResizeSections();
            _host.VisibleChanged += OnHostVisibleChanged;

            _empty = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(LabelLeft, GroupMargin + 4),
                Text = "No spectrometer detected."
            };

            _host.Controls.Add(_empty);
        }

        /// <summary>
        /// The sections are only created while the tab is actually on screen.
        ///
        /// Filling a tab page that is not the selected one leaves the tab control unable to
        /// hide it again, which shows up as the previous page staying painted over this
        /// one. Waiting until the page is up also keeps the device list from costing
        /// anything at start-up for an operator who never opens this tab.
        /// </summary>
        private void OnHostVisibleChanged(object sender, EventArgs e)
        {
            if (!_host.Visible || !_stale || !_host.IsHandleCreated)
                return;

            // Behind the tab switch that is in progress, not inside it.
            _host.BeginInvoke((MethodInvoker)delegate
            {
                if (!_host.Visible)
                    return;

                if (_stale)
                    Rebuild();

                RequestGpioState();
            });
        }

        /// <summary>
        /// Asks for each spectrometer's pin states to be read again.
        ///
        /// An input pin's level is set by whatever is wired to it, so the value read when
        /// the device was opened says nothing about now. This is not a poller - it runs
        /// when the tab is put on screen, which is the moment the numbers are looked at.
        /// </summary>
        private void RequestGpioState()
        {
            foreach (DeviceSection section in _sections)
            {
                if (section.Channels.Length == 0)
                    continue;

                RaiseChange(section, new HardwareChange { Kind = HardwareChangeKind.ReadGpio });
            }
        }

        /// <summary>Raised when the operator asks for a device setting to be changed.</summary>
        public event EventHandler<HardwareChangeRequestedEventArgs> ChangeRequested;

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Takes the current device list. Called from the same place the device table is
        /// rebuilt, so a spectrometer that is unplugged takes its section with it and
        /// cannot leave a control pointing at a device that is gone.
        ///
        /// The controls themselves are only created while the tab is on screen; see
        /// <see cref="OnHostVisibleChanged"/>.
        /// </summary>
        public void Build(DeviceInfo[] devices, DeviceSettings[] settings)
        {
            _devices = devices ?? new DeviceInfo[0];
            _settings = settings ?? new DeviceSettings[0];
            _stale = true;

            if (_host.Visible && _host.IsHandleCreated)
                Rebuild();
        }

        private void Rebuild()
        {
            _stale = false;

            DeviceInfo[] devices = _devices;
            DeviceSettings[] settings = _settings;

            _host.SuspendLayout();
            try
            {
                foreach (DeviceSection section in _sections)
                {
                    _host.Controls.Remove(section.Group);
                    section.Group.Dispose();
                }
                _sections.Clear();

                _empty.Visible = devices == null || devices.Length == 0;
                if (devices == null)
                    return;

                int top = GroupMargin;
                for (int i = 0; i < devices.Length; i++)
                {
                    DeviceSection section = BuildSection(devices[i], SettingsFor(settings, devices[i].Index));
                    section.Group.Top = top;
                    top += section.Group.Height + GroupMargin;

                    _sections.Add(section);
                    _host.Controls.Add(section.Group);
                }
            }
            finally
            {
                _host.ResumeLayout(true);
            }

            ResizeSections();
        }

        private static DeviceSettings SettingsFor(DeviceSettings[] settings, int deviceIndex)
        {
            if (settings == null)
                return null;

            for (int i = 0; i < settings.Length; i++)
                if (settings[i] != null && settings[i].Index == deviceIndex)
                    return settings[i];

            return null;
        }

        private DeviceSection BuildSection(DeviceInfo device, DeviceSettings settings)
        {
            var section = new DeviceSection
            {
                DeviceIndex = device.Index,
                Identity = device.Identity,
                Options = device.MeasurementModes,
                Channels = device.GpioChannels,
                Shown = settings == null ? TriggerMode.Normal : settings.TriggerMode
            };

            var group = new GroupBox
            {
                // The serial is in the caption so two connected spectrometers can be told
                // apart at a glance, not only by their position in the list.
                Text = "Spectrometer [" + device.Index + "]  -  " + device.ModelLabel +
                       (string.IsNullOrEmpty(device.SerialNumber) ? string.Empty : "  (" + device.SerialNumber + ")"),
                Left = GroupMargin,
                Width = Math.Max(560, _host.ClientSize.Width - 2 * GroupMargin - SystemInformation.VerticalScrollBarWidth),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            section.Group = group;

            int y = 22;
            AddPair(group, "Model", device.DetailLabel, ref y);
            AddPair(group, "Serial", string.IsNullOrEmpty(device.SerialNumber) ? "n/a" : device.SerialNumber, ref y);

            section.Role = AddPair(group, "Role", DescribeRole(settings), ref y);

            y += 4;
            group.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(LabelLeft, y + 3),
                Text = "Measurement Mode"
            });

            section.Mode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                Location = new Point(ValueLeft, y),
                Width = 320,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            section.Mode.DrawItem += OnDrawModeItem;
            section.Mode.SelectedIndexChanged += OnModeChanged;
            section.Mode.Tag = section;

            FillModes(section);
            group.Controls.Add(section.Mode);
            y += 28;

            section.ModeNote = new Label
            {
                ForeColor = SystemColors.GrayText,
                Location = new Point(LabelLeft, y),
                Size = new Size(group.Width - 2 * LabelLeft, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = DescribeModeSource(device)
            };
            group.Controls.Add(section.ModeNote);
            y += 38;

            group.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(LabelLeft, y),
                Text = "GPIO Channels"
            });
            y += 20;

            section.Gpio = BuildGpioGrid(section, device, group.Width);
            section.Gpio.Location = new Point(LabelLeft, y);
            group.Controls.Add(section.Gpio);
            y += section.Gpio.Height + 6;

            section.GpioNote = new Label
            {
                ForeColor = SystemColors.GrayText,
                Location = new Point(LabelLeft, y),
                Size = new Size(group.Width - 2 * LabelLeft, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = DescribeGpioSource(device)
            };
            group.Controls.Add(section.GpioNote);

            group.Height = y + 42;
            return section;
        }

        private static Label AddPair(Control parent, string caption, string value, ref int y)
        {
            parent.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(LabelLeft, y),
                Text = caption
            });

            var display = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.ControlText,
                Location = new Point(ValueLeft, y),
                Text = value
            };

            parent.Controls.Add(display);
            y += 21;
            return display;
        }

        private static string DescribeRole(DeviceSettings settings)
        {
            if (settings == null)
                return "unassigned";

            return settings.DescribeRole() + (settings.Enabled ? ", measured (Use)" : string.Empty);
        }

        /// <summary>
        /// Says where the mode list came from. OmniDriver has no call that enumerates the
        /// modes a spectrometer supports, so a list nothing was refused from is presented
        /// as exactly that rather than as a verified capability.
        /// </summary>
        private static string DescribeModeSource(DeviceInfo device)
        {
            int refused = 0;
            for (int i = 0; i < device.MeasurementModes.Length; i++)
                if (!device.MeasurementModes[i].Supported)
                    refused++;

            if (refused > 0)
            {
                return string.Format(CultureInfo.CurrentCulture,
                    "The driver refused {0} of these modes on this spectrometer; they are greyed out and cannot be picked.",
                    refused);
            }

            return "OmniDriver publishes no list of supported trigger modes. Each mode above was offered to " +
                   "this spectrometer when it was opened and the driver accepted it; any it refuses is greyed out.";
        }

        private static string DescribeGpioSource(DeviceInfo device)
        {
            if (!device.GpioSupported)
                return "This spectrometer does not expose the GPIO feature (isFeatureSupportedGPIO reports 0).";

            string text = string.Format(CultureInfo.CurrentCulture,
                "The driver reports {0} pin(s) and {1} GPIO bit(s) for this spectrometer.",
                device.GpioPinCount, device.GpioTotalBits);

            return text + " Choosing a direction routes that pin to general purpose I/O. Output pins are " +
                   "driven from the Output column; an input pin shows the level it reads.";
        }

        // ------------------------------------------------------------- measurement mode

        private void FillModes(DeviceSection section)
        {
            _suppress = true;
            try
            {
                section.Mode.Items.Clear();
                foreach (MeasurementModeOption option in section.Options)
                    section.Mode.Items.Add(option);

                section.Mode.Enabled = section.Mode.Items.Count > 0;
                SelectMode(section, section.Shown);
            }
            finally
            {
                _suppress = false;
            }
        }

        private static void SelectMode(DeviceSection section, TriggerMode mode)
        {
            for (int i = 0; i < section.Options.Length; i++)
            {
                if (section.Options[i].Mode != mode)
                    continue;

                section.Mode.SelectedIndex = i;
                section.Shown = mode;
                return;
            }

            section.Mode.SelectedIndex = section.Mode.Items.Count > 0 ? 0 : -1;
        }

        /// <summary>Greys out the modes the driver refused, which a combo box cannot do by itself.</summary>
        private void OnDrawModeItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            var combo = (ComboBox)sender;
            if (e.Index < 0 || e.Index >= combo.Items.Count)
                return;

            var option = combo.Items[e.Index] as MeasurementModeOption;
            if (option == null)
                return;

            Color color = option.Supported
                ? (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : SystemColors.ControlText
                : SystemColors.GrayText;

            string text = option.Supported ? option.Label : option.Label + "   (not available)";

            TextRenderer.DrawText(e.Graphics, text, e.Font, e.Bounds, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            e.DrawFocusRectangle();
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            if (_suppress)
                return;

            var combo = (ComboBox)sender;
            var section = (DeviceSection)combo.Tag;

            var option = combo.SelectedItem as MeasurementModeOption;
            if (option == null)
                return;

            if (!option.Supported)
            {
                // Visible so the operator can see the mode exists, but never selectable.
                Revert(section);
                Report("[" + section.DeviceIndex + "] " + option.Label + " is not available: " + option.Reason);
                return;
            }

            if (option.Mode == section.Shown)
                return;

            RaiseChange(section, new HardwareChange
            {
                Kind = HardwareChangeKind.MeasurementMode,
                Mode = option.Mode
            });
        }

        private void Revert(DeviceSection section)
        {
            _suppress = true;
            try { SelectMode(section, section.Shown); }
            finally { _suppress = false; }
        }

        // ------------------------------------------------------------------- GPIO grid

        private DataGridView BuildGpioGrid(DeviceSection section, DeviceInfo device, int groupWidth)
        {
            var grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 26,
                EditMode = DataGridViewEditMode.EditOnEnter,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ScrollBars = ScrollBars.None,
                Width = groupWidth - 2 * LabelLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Tag = section
            };

            grid.RowTemplate.Height = RowHeight;

            // A settings table, not a list to pick from - same choice as the device table.
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Window;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Channel",
                ReadOnly = true,
                FillWeight = 30f,
                MinimumWidth = 60
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Capability",
                ReadOnly = true,
                FillWeight = 45f,
                MinimumWidth = 80
            });
            grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Direction",
                FillWeight = 45f,
                MinimumWidth = 80,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            });
            grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Output",
                FillWeight = 40f,
                MinimumWidth = 70,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "State",
                ReadOnly = true,
                FillWeight = 90f,
                MinimumWidth = 120
            });

            for (int c = 0; c < grid.Columns.Count; c++)
            {
                if (!grid.Columns[c].ReadOnly)
                    continue;

                grid.Columns[c].DefaultCellStyle.BackColor = GridColors.ReadOnly;
                grid.Columns[c].DefaultCellStyle.SelectionBackColor = GridColors.ReadOnly;
            }

            grid.DataError += (s, e) => { e.ThrowException = false; e.Cancel = true; };
            grid.CurrentCellDirtyStateChanged += OnGpioDirtyStateChanged;
            grid.CellValueChanged += OnGpioCellValueChanged;

            FillGpioRows(grid, section.Channels);

            // No scroll bar: the panel behind the sections is what scrolls, so the table is
            // sized to show every channel the driver reported.
            grid.Height = grid.ColumnHeadersHeight + Math.Max(1, grid.RowCount) * RowHeight + 2;

            if (!device.GpioSupported)
                grid.Enabled = false;

            return grid;
        }

        private void FillGpioRows(DataGridView grid, GpioChannelInfo[] channels)
        {
            _suppress = true;
            try
            {
                grid.Rows.Clear();

                foreach (GpioChannelInfo channel in channels)
                {
                    int row = grid.Rows.Add("GPIO " + channel.Index, channel.DescribeCapability(),
                        null, null, channel.DescribeStatus());

                    ApplyDirectionCell(grid.Rows[row], channel);
                    ApplyOutputCell(grid.Rows[row], channel);
                }
            }
            finally
            {
                _suppress = false;
            }
        }

        /// <summary>
        /// Gives the row the only control that makes sense for that channel: a choice when
        /// the driver exposes both directions, plain text when it exposes one or none. A
        /// direction that cannot be chosen is shown greyed rather than left out, so the
        /// operator can see the channel exists.
        /// </summary>
        private static void ApplyDirectionCell(DataGridViewRow row, GpioChannelInfo channel)
        {
            if (channel.BothDirections)
            {
                var cell = new DataGridViewComboBoxCell
                {
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
                };
                cell.Items.Add("Input");
                cell.Items.Add("Output");
                cell.Value = channel.Direction == 1 ? "Output" : "Input";

                row.Cells[DirectionColumn] = cell;
                row.Cells[DirectionColumn].Tag = channel;
                Unlock(row, DirectionColumn);
                return;
            }

            Lock(row, DirectionColumn, channel.Usable ? channel.DescribeDirection() : "-", channel);

            if (!channel.Usable)
            {
                for (int c = 0; c < row.Cells.Count; c++)
                    row.Cells[c].Style.ForeColor = SystemColors.GrayText;
            }
        }

        /// <summary>
        /// The level control. It is only live on a pin that is an output right now: driving
        /// a level onto an input would mean silently changing its direction, and a pin the
        /// driver never offered as an output has nothing to drive at all.
        /// </summary>
        private static void ApplyOutputCell(DataGridViewRow row, GpioChannelInfo channel)
        {
            if (channel.IsOutput)
            {
                var cell = new DataGridViewComboBoxCell
                {
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
                };
                cell.Items.Add("LOW");
                cell.Items.Add("HIGH");
                cell.Value = channel.Value == 1 ? "HIGH" : "LOW";

                row.Cells[OutputColumn] = cell;
                row.Cells[OutputColumn].Tag = channel;

                // The cell this replaces was locked, and a cell that takes its place picks
                // that up: without clearing it the level chooser draws but cannot be opened,
                // which is what "I chose Output and then the level did nothing" looked like.
                Unlock(row, OutputColumn);
                return;
            }

            string text = !channel.Usable || !channel.OutputSupported
                ? "-"
                : "(set Direction to Output)";

            Lock(row, OutputColumn, text, channel);
        }

        /// <summary>
        /// Makes one cell editable again and gives it the ordinary look.
        ///
        /// A locked cell can leave the whole row marked read-only, and a cell that is put
        /// in its place inherits that, so a chooser has to say plainly that it is one.
        /// </summary>
        private static void Unlock(DataGridViewRow row, int column)
        {
            if (row.ReadOnly)
            {
                // Demote the row, then put the lock back on the cells that are meant to
                // stay read-only, so only this one becomes editable.
                row.ReadOnly = false;
                for (int c = 0; c < row.Cells.Count; c++)
                {
                    if (c != column && c != DirectionColumn && c != OutputColumn)
                        row.Cells[c].ReadOnly = true;
                }
            }

            DataGridViewCell cell = row.Cells[column];
            cell.ReadOnly = false;
            cell.Style.BackColor = SystemColors.Window;
            cell.Style.ForeColor = SystemColors.ControlText;
            cell.Style.SelectionBackColor = SystemColors.Window;
            cell.Style.SelectionForeColor = SystemColors.ControlText;
        }

        /// <summary>A cell that only reports; greyed so it does not read as a choice.</summary>
        private static void Lock(DataGridViewRow row, int column, string text, GpioChannelInfo channel)
        {
            row.Cells[column] = new DataGridViewTextBoxCell { Value = text };

            DataGridViewCell cell = row.Cells[column];
            cell.Tag = channel;
            cell.ReadOnly = true;
            cell.Style.BackColor = GridColors.Locked;
            cell.Style.ForeColor = SystemColors.GrayText;
            cell.Style.SelectionBackColor = GridColors.Locked;
            cell.Style.SelectionForeColor = SystemColors.GrayText;
        }

        private void OnGpioDirtyStateChanged(object sender, EventArgs e)
        {
            var grid = (DataGridView)sender;

            // A combo cell otherwise holds its new value back until the cell loses focus.
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        /// <summary>
        /// A direction or a level was picked. The request is addressed to the spectrometer
        /// this table belongs to - the section owns both the index and the identity - so a
        /// pin can only ever be written on the device whose section was touched.
        /// </summary>
        private void OnGpioCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppress || e.RowIndex < 0)
                return;

            if (e.ColumnIndex != DirectionColumn && e.ColumnIndex != OutputColumn)
                return;

            var grid = (DataGridView)sender;
            var section = (DeviceSection)grid.Tag;

            var channel = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag as GpioChannelInfo;
            if (channel == null || !channel.Usable)
                return;

            string picked = Convert.ToString(
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, CultureInfo.InvariantCulture);

            if (e.ColumnIndex == DirectionColumn)
            {
                bool output = string.Equals(picked, "Output", StringComparison.Ordinal);
                if ((channel.Direction == 1) == output)
                    return;

                RaiseChange(section, new HardwareChange
                {
                    Kind = HardwareChangeKind.GpioDirection,
                    Pin = channel.Index,
                    Output = output
                });
                return;
            }

            bool high = string.Equals(picked, "HIGH", StringComparison.Ordinal);
            if ((channel.Value == 1) == high)
                return;

            RaiseChange(section, new HardwareChange
            {
                Kind = HardwareChangeKind.GpioLevel,
                Pin = channel.Index,
                High = high
            });
        }

        // ------------------------------------------------------------------- feedback

        /// <summary>
        /// Takes the result of a change back into the controls. A setting the driver did
        /// not accept is put back to what the device actually holds, so the tab can never
        /// show a configuration that is not in force.
        /// </summary>
        public void ApplyResult(HardwareAppliedEventArgs result)
        {
            // Matched on identity, not index: a result that arrives after the device list
            // changed must not be written into whichever section now sits at that index.
            DeviceSection section = Find(result.Change.DeviceIdentity);
            if (section == null)
                return;

            if (result.Change.Kind == HardwareChangeKind.MeasurementMode)
            {
                if (result.Applied)
                    section.Shown = result.Change.Mode;

                Revert(section);   // to Shown, which is either the new mode or the old one
            }

            if (result.GpioChannels != null)
                RefreshGpio(section, result.GpioChannels);
        }

        /// <summary>
        /// Re-reads the GPIO rows from what the driver reports now.
        ///
        /// The rows are updated in place rather than rebuilt. Clearing them - or swapping a
        /// cell for a different kind of cell - while one of them is still the cell being
        /// edited throws out of DataGridView, and an exception raised while it paints
        /// leaves the table drawn as a red cross for the rest of the session. This runs
        /// right after the operator picked a direction, so that cell usually *is* in edit
        /// mode; ending the edit first is what makes the update safe.
        /// </summary>
        private void RefreshGpio(DeviceSection section, GpioChannelInfo[] channels)
        {
            section.Channels = channels;

            DataGridView grid = section.Gpio;
            if (grid == null || grid.IsDisposed)
                return;

            _suppress = true;
            try
            {
                grid.EndEdit();

                if (grid.RowCount != channels.Length)
                {
                    // The channel list itself changed, so the rows have to be rebuilt. Safe
                    // here only because the edit above is finished and no cell is current.
                    grid.CurrentCell = null;
                    FillGpioRows(grid, channels);
                    return;
                }

                bool replaced = false;

                for (int row = 0; row < channels.Length; row++)
                {
                    GpioChannelInfo channel = channels[row];
                    DataGridViewRow line = grid.Rows[row];

                    line.Cells[0].Value = "GPIO " + channel.Index;
                    line.Cells[1].Value = channel.DescribeCapability();
                    line.Cells[StateColumn].Value = channel.DescribeStatus();
                    line.Cells[DirectionColumn].Tag = channel;
                    line.Cells[OutputColumn].Tag = channel;

                    if (channel.BothDirections)
                        line.Cells[DirectionColumn].Value = channel.Direction == 1 ? "Output" : "Input";
                    else if (channel.Usable)
                        line.Cells[DirectionColumn].Value = channel.DescribeDirection();

                    // A pin that changed direction needs the other kind of cell in the
                    // Output column, and swapping a cell is only safe once the edit above
                    // has finished - which is why EndEdit() runs first.
                    bool isChooser = line.Cells[OutputColumn] is DataGridViewComboBoxCell;
                    if (isChooser != channel.IsOutput)
                    {
                        ApplyOutputCell(line, channel);
                        replaced = true;
                    }
                    else if (channel.IsOutput)
                    {
                        line.Cells[OutputColumn].Value = channel.Value == 1 ? "HIGH" : "LOW";
                    }
                }

                // A cell that was swapped for a different kind of cell leaves the grid
                // still pointing at the row it was editing, and the next click there only
                // dismisses that leftover editor instead of opening the new one - so
                // choosing Output and then reaching for the level did nothing. Letting go
                // of the current cell makes the next click a fresh one.
                if (replaced)
                    grid.CurrentCell = null;
            }
            finally
            {
                _suppress = false;
            }
        }

        /// <summary>Refreshes the role line after the device table changes.</summary>
        public void ShowRoles(DeviceSettings[] settings)
        {
            foreach (DeviceSection section in _sections)
            {
                if (section.Role != null)
                    section.Role.Text = DescribeRole(SettingsFor(settings, section.DeviceIndex));
            }
        }

        private DeviceSection Find(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                return null;

            foreach (DeviceSection section in _sections)
                if (string.Equals(section.Identity, identity, StringComparison.Ordinal))
                    return section;

            return null;
        }

        private void ResizeSections()
        {
            int width = Math.Max(560,
                _host.ClientSize.Width - 2 * GroupMargin - SystemInformation.VerticalScrollBarWidth);

            foreach (DeviceSection section in _sections)
            {
                section.Group.Width = width;
                if (section.Gpio != null)
                    section.Gpio.Width = width - 2 * LabelLeft;
            }
        }

        /// <summary>Raised for things worth putting in the status bar.</summary>
        public event EventHandler<ChartMessageEventArgs> Reported;

        /// <summary>
        /// Addresses a request to the spectrometer the section belongs to. Filling both the
        /// index and the identity here - rather than at each call site - is what keeps a
        /// control from ever naming a device other than its own.
        /// </summary>
        private void RaiseChange(DeviceSection section, HardwareChange change)
        {
            change.DeviceIndex = section.DeviceIndex;
            change.DeviceIdentity = section.Identity;

            EventHandler<HardwareChangeRequestedEventArgs> handler = ChangeRequested;
            if (handler != null)
                handler(this, new HardwareChangeRequestedEventArgs(change));
        }

        private void Report(string message)
        {
            EventHandler<ChartMessageEventArgs> handler = Reported;
            if (handler != null)
                handler(this, new ChartMessageEventArgs(message));
        }
    }
}
