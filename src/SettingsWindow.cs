using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Stagehand
{
    sealed class SettingsWindow : Window
    {
        const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Background='#0C0C0E' Padding='28,24,28,24' TextElement.FontFamily='Segoe UI Variable Text, Segoe UI' TextElement.Foreground='#E8E8EA'>
" + GraphiteXaml.Resources + @"
  <DockPanel>
    <StackPanel DockPanel.Dock='Top'>
      <TextBlock Text='Stagehand' FontSize='24' FontWeight='SemiBold'/>
      <StackPanel Orientation='Horizontal' Margin='0,6,0,20'>
        <Ellipse x:Name='StatusDot' Width='8' Height='8' Margin='0,0,8,0' VerticalAlignment='Center'/>
        <TextBlock x:Name='StatusText' Style='{StaticResource Hint}'/>
      </StackPanel>
    </StackPanel>

    <DockPanel DockPanel.Dock='Bottom' Margin='0,6,0,0' LastChildFill='False'>
      <Button x:Name='ExitButton' Content='Stop Stagehand' Style='{StaticResource Danger}' DockPanel.Dock='Left'/>
      <Button x:Name='SaveButton' Content='Save' Style='{StaticResource Primary}' DockPanel.Dock='Right'/>
      <Button x:Name='CancelButton' Content='Cancel' Style='{StaticResource Quiet}' DockPanel.Dock='Right' Margin='0,0,8,0'/>
    </DockPanel>

    <ScrollViewer VerticalScrollBarVisibility='Auto' Margin='0,0,-16,0' Padding='0,0,16,0'>
    <StackPanel>

    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Case' Style='{StaticResource Heading}'/>
        <TextBlock Text='These switch right away, without Save, and stay as you leave them after a restart.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='RGB lighting' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='LightsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='Everything L-Connect 3 lights, including the motherboard, GPU and RAM through OpenRGB. Fan and pump speeds and the screens stay as they are.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='Screens' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='ScreensToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='The cooler, LCD fan and 9.2-inch screens turn off. The 9.2-inch screen leaves Windows display mode while off, so switching takes a few seconds. RGB stays as it is.' Style='{StaticResource Hint}'/>
        <TextBlock x:Name='CaseError' Style='{StaticResource Hint}' Foreground='#F2A09A' TextWrapping='Wrap' Margin='0,12,0,0' Visibility='Collapsed'/>
      </StackPanel>
    </Border>

    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Idle background' Style='{StaticResource Heading}'/>
        <TextBlock Text='Plays when nothing is playing. Video or image; it fills the 1920 × 464 screen.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <Border x:Name='PreviewFrame' Width='488' Height='118' CornerRadius='10' BorderBrush='#26262C' BorderThickness='1' Background='#0C0C0E' HorizontalAlignment='Left'/>
        <DockPanel Margin='0,14,0,0' LastChildFill='False'>
          <Button x:Name='ChooseButton' Content='Choose file…' Style='{StaticResource Secondary}' DockPanel.Dock='Left'/>
          <Button x:Name='ResetButton' Content='Use default' Style='{StaticResource Quiet}' DockPanel.Dock='Left' Margin='6,0,0,0'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='AnchorTop' Content='Top' GroupName='Anchor' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='AnchorCenter' Content='Center' GroupName='Anchor' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='AnchorBottom' Content='Bottom' GroupName='Anchor' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
          <TextBlock Text='Crop' Style='{StaticResource Hint}' VerticalAlignment='Center' Margin='0,0,10,0' DockPanel.Dock='Right'/>
        </DockPanel>
      </StackPanel>
    </Border>

    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Lyrics' Style='{StaticResource Heading}' Margin='0,0,0,14'/>
        <DockPanel>
          <TextBlock Text='Timing offset' Style='{StaticResource Label}' Width='150'/>
          <TextBlock x:Name='OffsetValue' DockPanel.Dock='Right' Width='64' TextAlignment='Right' FontFamily='Cascadia Mono, Consolas' FontSize='13' VerticalAlignment='Center'/>
          <Slider x:Name='OffsetSlider' Minimum='-1' Maximum='1.5' SmallChange='0.05' LargeChange='0.25' Style='{StaticResource Range}' Margin='0,0,12,0'/>
        </DockPanel>
        <TextBlock Text='Raise it if lines light up late, lower it if they run early.' Style='{StaticResource Hint}' Margin='150,4,0,14'/>
        <DockPanel LastChildFill='False' Margin='0,0,0,6'>
          <TextBlock Text='Romanized lyrics' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='RomanizeOff' Content='Off' GroupName='Romanize' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='RomanizeBelow' Content='Below' GroupName='Romanize' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='RomanizeOnly' Content='Only' GroupName='Romanize' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <TextBlock Text='Japanese and Korean. Below adds a smaller line under each lyric; Only replaces it.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
        <DockPanel>
          <TextBlock Text='Song stats panel' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='StatsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
      </StackPanel>
    </Border>

    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Behaviour' Style='{StaticResource Heading}' Margin='0,0,0,14'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Keep the mouse off the lyrics screen' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='MouseToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Start with Windows' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='StartupToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='Show tray icon' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='TrayToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='Without it, open Stagehand from the Start menu to change settings or stop it.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
        <DockPanel LastChildFill='False'>
          <TextBlock Text='Return to the idle loop after pausing' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='Idle5' Content='5 s' GroupName='Idle' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='Idle20' Content='20 s' GroupName='Idle' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='Idle60' Content='1 min' GroupName='Idle' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
      </StackPanel>
    </Border>

    </StackPanel>
    </ScrollViewer>
  </DockPanel>
</Border>";

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly Settings saved;
        readonly Settings draft;
        readonly CaseSwitches switches = CaseSwitches.Load();
        readonly IdleBackground preview = new IdleBackground { Width = 486, Height = 116 };
        readonly FrameworkElement root;
        bool startWithWindows;
        string previewPath, previewAnchor;

        public SettingsWindow(Settings settings)
        {
            saved = settings;
            draft = settings.Copy();
            startWithWindows = StartupShortcut.Exists;
            Title = "Stagehand";
            Width = 600;
            SizeToContent = SizeToContent.Height;
            MaxHeight = SystemParameters.WorkArea.Height - 48;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Graphite.Brush(Graphite.Paper);
            root = (FrameworkElement)XamlReader.Parse(Xaml);
            Content = root;

            var frame = Find<Border>("PreviewFrame");
            preview.Clip = new RectangleGeometry(new Rect(0, 0, 486, 116), 9, 9);
            frame.Child = preview;

            Find<Button>("ChooseButton").Click += (s, e) => ChooseBackground();
            Find<Button>("ResetButton").Click += (s, e) => { draft.IdleBackgroundPath = ""; Find<RadioButton>("AnchorCenter").IsChecked = true; Changed(); };
            Choice("Anchor", new[] { "Top", "Center", "Bottom" }, draft.IdleAnchor, value => draft.IdleAnchor = value);

            var slider = Find<Slider>("OffsetSlider");
            slider.Value = draft.LyricOffsetSeconds;
            ShowOffset(draft.LyricOffsetSeconds);
            slider.ValueChanged += (s, e) =>
            {
                draft.LyricOffsetSeconds = Math.Round(e.NewValue / 0.05) * 0.05;
                ShowOffset(draft.LyricOffsetSeconds);
                Changed();
            };

            Choice("Romanize", new[] { "Off", "Below", "Only" }, draft.RomanizeMode, value => draft.RomanizeMode = value);
            Toggle("StatsToggle", draft.ShowStats, value => draft.ShowStats = value);
            Toggle("MouseToggle", draft.KeepMouseOff, value => draft.KeepMouseOff = value);
            Toggle("TrayToggle", draft.ShowTrayIcon, value => draft.ShowTrayIcon = value);
            Toggle("StartupToggle", startWithWindows, value => startWithWindows = value);
            Choice("Idle", new[] { "5", "20", "60" }, draft.IdleAfterPauseSeconds.ToString(), value => draft.IdleAfterPauseSeconds = int.Parse(value));

            CaseSwitch("LightsToggle", switches.LightsOff, switches.SetLightsOffAsync);
            CaseSwitch("ScreensToggle", switches.ScreensOff, switches.SetScreensOffAsync);

            Find<Button>("SaveButton").Click += (s, e) => Save();
            Find<Button>("CancelButton").Click += (s, e) => Close();
            Find<Button>("ExitButton").Click += (s, e) => { Program.StopAgent(); Close(); };

            var screen = ScreenPlacement.FindLyricsScreen();
            Find<TextBlock>("StatusText").Text = screen != null
                ? "Showing on the 9.2-inch screen (" + screen.Bounds.Width + " × " + screen.Bounds.Height + ")"
                : "9.2-inch screen not found. Turn on Secondary screen for it in L-Connect 3.";
            Find<System.Windows.Shapes.Ellipse>("StatusDot").Fill = Graphite.Brush(screen != null ? Graphite.Live : Graphite.Wip);

            SourceInitialized += (s, e) =>
            {
                var dark = 1;
                DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
            };
            Closed += (s, e) => preview.SetPlaying(false);
            Changed();
        }

        T Find<T>(string name) where T : class => (T)root.FindName(name);

        void Toggle(string name, bool value, Action<bool> set)
        {
            var toggle = Find<ToggleButton>(name);
            toggle.IsChecked = value;
            toggle.Checked += (s, e) => { set(true); Changed(); };
            toggle.Unchecked += (s, e) => { set(false); Changed(); };
        }

        void CaseSwitch(string name, bool off, Func<bool, Task> setOff)
        {
            var toggle = Find<ToggleButton>(name);
            toggle.IsChecked = !off;
            var reverting = false;
            RoutedEventHandler flipped = async (s, e) =>
            {
                if (reverting) return;
                var error = Find<TextBlock>("CaseError");
                toggle.IsEnabled = false;
                try
                {
                    await setOff(toggle.IsChecked != true);
                    error.Visibility = Visibility.Collapsed;
                }
                catch (Exception failure) when (failure is HttpRequestException || failure is TaskCanceledException || failure is InvalidOperationException)
                {
                    Log.Write("case switch: " + failure);
                    error.Text = "Couldn't switch: " + failure.Message;
                    error.Visibility = Visibility.Visible;
                    reverting = true;
                    toggle.IsChecked = toggle.IsChecked != true;
                    reverting = false;
                }
                toggle.IsEnabled = true;
            };
            toggle.Checked += flipped;
            toggle.Unchecked += flipped;
        }

        void Choice(string prefix, string[] options, string current, Action<string> set)
        {
            foreach (var option in options)
            {
                var radio = Find<RadioButton>(prefix + option);
                radio.IsChecked = current == option;
                radio.Checked += (s, e) => { set(option); Changed(); };
            }
        }

        void ShowOffset(double seconds) => Find<TextBlock>("OffsetValue").Text = (seconds >= 0 ? "+" : "") + seconds.ToString("0.00") + " s";

        void ChooseBackground()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an idle background",
                Filter = "Video or image|*.mp4;*.m4v;*.mov;*.wmv;*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
            };
            if (dialog.ShowDialog(this) != true) return;
            draft.IdleBackgroundPath = dialog.FileName;
            Changed();
        }

        void Changed()
        {
            if (draft.IdleBackgroundPath != previewPath || draft.IdleAnchor != previewAnchor)
            {
                previewPath = draft.IdleBackgroundPath;
                previewAnchor = draft.IdleAnchor;
                preview.Load(previewPath, previewAnchor);
                preview.SetPlaying(true);
            }
            var dirty = !draft.SameAs(saved) || startWithWindows != StartupShortcut.Exists;
            var save = Find<Button>("SaveButton");
            save.IsEnabled = dirty;
            save.Content = dirty ? "Save" : "Saved";
        }

        void Save()
        {
            saved.CopyFrom(draft);
            saved.Save();
            if (startWithWindows && !StartupShortcut.Exists) StartupShortcut.Create();
            if (!startWithWindows && StartupShortcut.Exists) StartupShortcut.Remove();
            Changed();
        }
    }
}