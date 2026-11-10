using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace GraphiteLyrics
{
    sealed class SettingsWindow : Window
    {
        const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Background='#0C0C0E' Padding='28,24,28,24' TextElement.FontFamily='Segoe UI Variable Text, Segoe UI' TextElement.Foreground='#E8E8EA'>
  <Border.Resources>
    <Style x:Key='Panel' TargetType='Border'>
      <Setter Property='Background' Value='#131316'/><Setter Property='BorderBrush' Value='#26262C'/><Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='12'/><Setter Property='Padding' Value='20,18,20,18'/><Setter Property='Margin' Value='0,0,0,14'/>
    </Style>
    <Style x:Key='Heading' TargetType='TextBlock'><Setter Property='FontSize' Value='15'/><Setter Property='FontWeight' Value='SemiBold'/></Style>
    <Style x:Key='Hint' TargetType='TextBlock'><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#8B8B95'/><Setter Property='TextWrapping' Value='Wrap'/></Style>
    <Style x:Key='Label' TargetType='TextBlock'><Setter Property='FontSize' Value='14'/><Setter Property='VerticalAlignment' Value='Center'/></Style>
    <Style x:Key='Secondary' TargetType='Button'>
      <Setter Property='Foreground' Value='#E8E8EA'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
        <Border x:Name='b' Background='Transparent' BorderBrush='#26262C' BorderThickness='1' CornerRadius='8' Padding='14,7'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.FontSize='14'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#1C1C21'/><Setter TargetName='b' Property='BorderBrush' Value='#8B8B95'/></Trigger>
          <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#26262C'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Primary' TargetType='Button'>
      <Setter Property='Foreground' Value='#0C0C0E'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
        <Border x:Name='b' Background='#E8E8EA' CornerRadius='8' Padding='18,8'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.FontSize='14' TextElement.FontWeight='SemiBold'/>
        </Border>
        <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#FFFFFF'/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Quiet' TargetType='Button'>
      <Setter Property='Foreground' Value='#8B8B95'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
        <Border x:Name='b' Background='Transparent' CornerRadius='8' Padding='12,7'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.FontSize='14'/>
        </Border>
        <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='#E8E8EA'/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Danger' TargetType='Button' BasedOn='{StaticResource Quiet}'><Setter Property='Foreground' Value='#F2A09A'/></Style>
    <Style x:Key='Switch' TargetType='ToggleButton'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToggleButton'>
        <Grid Width='40' Height='22' Background='Transparent'>
          <Border x:Name='track' CornerRadius='11' Background='#26262C'/>
          <Ellipse x:Name='thumb' Width='16' Height='16' Margin='3,0,3,0' Fill='#8B8B95' HorizontalAlignment='Left'/>
        </Grid>
        <ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'>
          <Setter TargetName='track' Property='Background' Value='#E8E8EA'/><Setter TargetName='thumb' Property='Fill' Value='#0C0C0E'/><Setter TargetName='thumb' Property='HorizontalAlignment' Value='Right'/>
        </Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Segment' TargetType='RadioButton'>
      <Setter Property='Foreground' Value='#8B8B95'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'>
        <Border x:Name='b' Background='Transparent' CornerRadius='6' Padding='12,5'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.FontSize='13'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter Property='Foreground' Value='#E8E8EA'/></Trigger>
          <Trigger Property='IsChecked' Value='True'><Setter TargetName='b' Property='Background' Value='#26262C'/><Setter Property='Foreground' Value='#E8E8EA'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Segments' TargetType='Border'><Setter Property='BorderBrush' Value='#26262C'/><Setter Property='BorderThickness' Value='1'/><Setter Property='CornerRadius' Value='8'/><Setter Property='Padding' Value='3'/></Style>
    <Style x:Key='Invisible' TargetType='RepeatButton'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter></Style>
    <Style x:Key='Range' TargetType='Slider'>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'>
        <Grid Height='22'>
          <Border Height='4' CornerRadius='2' Background='#26262C' VerticalAlignment='Center'/>
          <Track x:Name='PART_Track'>
            <Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Style='{StaticResource Invisible}'/></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Style='{StaticResource Invisible}'/></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb Cursor='Hand'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Width='16' Height='16' Fill='#E8E8EA'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
  </Border.Resources>
  <StackPanel>
    <TextBlock Text='Graphite Lyrics' FontSize='24' FontWeight='SemiBold'/>
    <StackPanel Orientation='Horizontal' Margin='0,6,0,20'>
      <Ellipse x:Name='StatusDot' Width='8' Height='8' Margin='0,0,8,0' VerticalAlignment='Center'/>
      <TextBlock x:Name='StatusText' Style='{StaticResource Hint}'/>
    </StackPanel>

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
        <TextBlock Text='Japanese and Korean. Below adds a smaller line under each lyric; Only replaces it.' Style='{StaticResource Hint}' Margin='0,0,0,4'/>
        <TextBlock x:Name='KanjiHint' Style='{StaticResource Hint}' Foreground='#D7A54A' Margin='0,0,0,14'/>
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

    <DockPanel Margin='0,6,0,0' LastChildFill='False'>
      <Button x:Name='ExitButton' Content='Stop lyrics screen' Style='{StaticResource Danger}' DockPanel.Dock='Left'/>
      <Button x:Name='DoneButton' Content='Done' Style='{StaticResource Primary}' DockPanel.Dock='Right'/>
    </DockPanel>
  </StackPanel>
</Border>";

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly Settings settings;
        readonly IdleBackground preview = new IdleBackground { Width = 486, Height = 116 };
        readonly FrameworkElement root;
        bool loading = true;

        public SettingsWindow(Settings settings)
        {
            this.settings = settings;
            Title = "Graphite Lyrics";
            Width = 600;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Graphite.Brush(Graphite.Paper);
            root = (FrameworkElement)XamlReader.Parse(Xaml);
            Content = root;

            var frame = Find<Border>("PreviewFrame");
            preview.Clip = new RectangleGeometry(new Rect(0, 0, 486, 116), 9, 9);
            frame.Child = preview;

            Find<Button>("ChooseButton").Click += (s, e) => ChooseBackground();
            Find<Button>("ResetButton").Click += (s, e) => { settings.IdleBackgroundPath = ""; settings.IdleAnchor = "Center"; Find<RadioButton>("AnchorCenter").IsChecked = true; Commit(); };
            foreach (var anchor in new[] { "Top", "Center", "Bottom" })
            {
                var radio = Find<RadioButton>("Anchor" + anchor);
                radio.IsChecked = settings.IdleAnchor == anchor;
                radio.Checked += (s, e) => { settings.IdleAnchor = anchor; Commit(); };
            }

            var slider = Find<Slider>("OffsetSlider");
            slider.Value = settings.LyricOffsetSeconds;
            ShowOffset(settings.LyricOffsetSeconds);
            slider.ValueChanged += (s, e) =>
            {
                settings.LyricOffsetSeconds = Math.Round(e.NewValue / 0.05) * 0.05;
                ShowOffset(settings.LyricOffsetSeconds);
                Commit();
            };

            foreach (var mode in new[] { "Off", "Below", "Only" })
            {
                var radio = Find<RadioButton>("Romanize" + mode);
                radio.IsChecked = settings.RomanizeMode == mode;
                radio.Checked += (s, e) => { settings.RomanizeMode = mode; Commit(); };
            }
            var kanjiHint = Find<TextBlock>("KanjiHint");
            kanjiHint.Text = Romanizer.HasJapaneseReadings ? "" : "Kanji stay as written until Japanese is added in Windows Settings › Time & language › Language & region.";
            kanjiHint.Visibility = kanjiHint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            Bind("StatsToggle", settings.ShowStats, value => settings.ShowStats = value);
            Bind("MouseToggle", settings.KeepMouseOff, value => settings.KeepMouseOff = value);
            foreach (var seconds in new[] { 5, 20, 60 })
            {
                var radio = Find<RadioButton>("Idle" + seconds);
                radio.IsChecked = settings.IdleAfterPauseSeconds == seconds;
                radio.Checked += (s, e) => { settings.IdleAfterPauseSeconds = seconds; Commit(); };
            }

            Find<Button>("DoneButton").Click += (s, e) => Close();
            Find<Button>("ExitButton").Click += (s, e) => { Program.StopAgent(); Close(); };
            var startup = Find<ToggleButton>("StartupToggle");
            startup.IsChecked = StartupShortcut.Exists;
            startup.Checked += (s, e) => StartupShortcut.Create();
            startup.Unchecked += (s, e) => StartupShortcut.Remove();

            var screen = ScreenPlacement.FindLyricsScreen();
            Find<TextBlock>("StatusText").Text = screen != null
                ? "Showing on the 9.2-inch screen (" + screen.Bounds.Width + " × " + screen.Bounds.Height + ")"
                : "9.2-inch screen not found. Turn on Secondary screen for it in L-Connect 3.";
            Find<System.Windows.Shapes.Ellipse>("StatusDot").Fill = Graphite.Brush(screen != null ? Graphite.Live : Graphite.Wip);

            preview.Load(settings.IdleBackgroundPath, settings.IdleAnchor);
            preview.SetPlaying(true);
            SourceInitialized += (s, e) =>
            {
                var dark = 1;
                DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
            };
            Closed += (s, e) => preview.SetPlaying(false);
            loading = false;
        }

        T Find<T>(string name) where T : class => (T)root.FindName(name);

        void Bind(string name, bool value, Action<bool> set)
        {
            var toggle = Find<ToggleButton>(name);
            toggle.IsChecked = value;
            toggle.Checked += (s, e) => { set(true); Commit(); };
            toggle.Unchecked += (s, e) => { set(false); Commit(); };
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
            settings.IdleBackgroundPath = dialog.FileName;
            Commit();
        }

        void Commit()
        {
            if (loading) return;
            settings.Save();
            preview.Load(settings.IdleBackgroundPath, settings.IdleAnchor);
            preview.SetPlaying(true);
        }
    }
}
