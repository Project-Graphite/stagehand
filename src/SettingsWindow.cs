using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

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
      <Border BorderBrush='#1C1C21' BorderThickness='0,0,0,1' Margin='0,0,0,18'>
        <StackPanel Orientation='Horizontal'>
          <RadioButton x:Name='TabCase' Content='Case' GroupName='Tab' Style='{StaticResource Tab}' Visibility='Collapsed'/>
          <RadioButton x:Name='TabLyrics' Content='Lyrics' GroupName='Tab' Style='{StaticResource Tab}'/>
          <RadioButton x:Name='TabLook' Content='Look' GroupName='Tab' Style='{StaticResource Tab}'/>
          <RadioButton x:Name='TabIdle' Content='Idle' GroupName='Tab' Style='{StaticResource Tab}'/>
          <RadioButton x:Name='TabRoutines' Content='Routines' GroupName='Tab' Style='{StaticResource Tab}'/>
          <RadioButton x:Name='TabGaming' Content='Gaming' GroupName='Tab' Style='{StaticResource Tab}'/>
          <RadioButton x:Name='TabGeneral' Content='General' GroupName='Tab' Style='{StaticResource Tab}'/>
        </StackPanel>
      </Border>
    </StackPanel>

    <DockPanel DockPanel.Dock='Bottom' Margin='0,6,0,0' LastChildFill='False'>
      <Button x:Name='ExitButton' Content='Stop Stagehand' Style='{StaticResource Danger}' DockPanel.Dock='Left'/>
      <Button x:Name='SaveButton' Content='Save' Style='{StaticResource Primary}' DockPanel.Dock='Right'/>
      <Button x:Name='CancelButton' Content='Cancel' Style='{StaticResource Quiet}' DockPanel.Dock='Right' Margin='0,0,8,0'/>
    </DockPanel>

    <ScrollViewer x:Name='Pages' VerticalScrollBarVisibility='Auto' Margin='0,0,-16,0' Padding='0,0,16,0'>
    <Grid>
    <StackPanel x:Name='PageCase' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Case' Style='{StaticResource Heading}'/>
        <TextBlock Text='These switch right away, without Save, and stay as you leave them after a restart.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='RGB lighting' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='LightsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='The lights picked under Case devices, from Lian Li fans to the motherboard, GPU and RAM through OpenRGB. Fan and pump speeds stay as they are.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='Screens' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='ScreensToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='The screens picked under Case devices. The 9.2-inch case screen leaves Windows display mode while off, so switching it takes a few seconds.' Style='{StaticResource Hint}'/>
        <TextBlock x:Name='CaseError' Style='{StaticResource Hint}' Foreground='#F2A09A' TextWrapping='Wrap' Margin='0,12,0,0' Visibility='Collapsed'/>
      </StackPanel>
    </Border>

    <Border x:Name='DevicesPanel' Style='{StaticResource Panel}' Visibility='Collapsed'>
      <StackPanel>
        <TextBlock Text='Case devices' Style='{StaticResource Heading}'/>
        <TextBlock Text='What each case switch turns off. Applies the next time you flip the switch.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <TextBlock Text='RGB lighting' Style='{StaticResource Label}' FontWeight='SemiBold' Margin='0,0,0,6'/>
        <StackPanel x:Name='LightDevices' Margin='0,0,0,14'/>
        <TextBlock Text='Screens' Style='{StaticResource Label}' FontWeight='SemiBold' Margin='0,0,0,6'/>
        <StackPanel x:Name='ScreenDevices'/>
      </StackPanel>
    </Border>
    </StackPanel>

    <StackPanel x:Name='PageLook' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Screen and look' Style='{StaticResource Heading}' Margin='0,0,0,14'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Lyrics screen' Style='{StaticResource Label}' Width='150'/>
          <ComboBox x:Name='DisplayPick' Style='{StaticResource Pick}'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Font' Style='{StaticResource Label}' Width='150'/>
          <ComboBox x:Name='FontPick' Style='{StaticResource Pick}'/>
        </DockPanel>
        <DockPanel LastChildFill='False' Margin='0,0,0,14'>
          <TextBlock Text='Text size' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='SizeS' Content='S' GroupName='Size' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='SizeM' Content='M' GroupName='Size' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='SizeL' Content='L' GroupName='Size' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='SizeXL' Content='XL' GroupName='Size' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <DockPanel LastChildFill='False' Margin='0,0,0,14'>
          <TextBlock Text='Alignment' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='AlignLeft' Content='Left' GroupName='Align' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='AlignCenter' Content='Center' GroupName='Align' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <DockPanel LastChildFill='False' Margin='0,0,0,14'>
          <TextBlock Text='Highlight colour' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='ColorWhite' Content='White' GroupName='Color' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='ColorCover' Content='From cover' GroupName='Color' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <DockPanel LastChildFill='False' Margin='0,0,0,14'>
          <TextBlock Text='Background while playing' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='BackgroundCover' Content='Cover' GroupName='Background' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='BackgroundIdle' Content='Idle loop' GroupName='Background' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Cover art and title' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='CoverToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='Audio visualizer' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='VisualizerToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='Bars behind the lyrics that move with whatever Windows is playing, in the colour of the cover.' Style='{StaticResource Hint}'/>
      </StackPanel>
    </Border>
    </StackPanel>

    <StackPanel x:Name='PageIdle' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Idle background' Style='{StaticResource Heading}'/>
        <TextBlock Text='Plays when nothing is playing. Video or image; it fills the lyrics screen.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
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
    </StackPanel>

    <StackPanel x:Name='PageLyrics' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Lyrics' Style='{StaticResource Heading}' Margin='0,0,0,14'/>
        <DockPanel>
          <TextBlock Text='Timing offset' Style='{StaticResource Label}' Width='150'/>
          <TextBlock x:Name='OffsetValue' DockPanel.Dock='Right' Width='64' TextAlignment='Right' FontFamily='Cascadia Mono, Consolas' FontSize='13' VerticalAlignment='Center'/>
          <Slider x:Name='OffsetSlider' Minimum='-1' Maximum='1.5' SmallChange='0.05' LargeChange='0.25' Style='{StaticResource Range}' Margin='0,0,12,0'/>
        </DockPanel>
        <TextBlock Text='Raise it if lines light up late, lower it if they run early.' Style='{StaticResource Hint}' Margin='150,4,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='This song' Style='{StaticResource Label}' Width='150'/>
          <Button x:Name='NudgeLater' Content='Later' Style='{StaticResource Secondary}' DockPanel.Dock='Right'/>
          <Button x:Name='NudgeEarlier' Content='Earlier' Style='{StaticResource Secondary}' DockPanel.Dock='Right' Margin='0,0,6,0'/>
          <TextBlock x:Name='NudgeValue' DockPanel.Dock='Right' FontFamily='Cascadia Mono, Consolas' FontSize='13' VerticalAlignment='Center' Margin='12,0,12,0'/>
          <TextBlock x:Name='NudgeSong' Style='{StaticResource Hint}' VerticalAlignment='Center' TextWrapping='NoWrap' TextTrimming='CharacterEllipsis'/>
        </DockPanel>
        <TextBlock Text='Moves the lyrics of the song playing now, right away, and remembers it for that song.' Style='{StaticResource Hint}' Margin='150,0,0,14'/>
        <DockPanel LastChildFill='False' Margin='0,0,0,6'>
          <TextBlock Text='Highlight' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='HighlightLines' Content='Lines' GroupName='Highlight' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='HighlightWords' Content='Words' GroupName='Highlight' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
        <TextBlock Text='Lines lights each line as it starts. Words estimates each word from its syllables. Lyrics with word timings always go word by word.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
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
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Song stats panel' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='StatsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel LastChildFill='False'>
          <TextBlock Text='Stats panel size' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='StatsS' Content='S' GroupName='StatsSize' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='StatsM' Content='M' GroupName='StatsSize' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='StatsL' Content='L' GroupName='StatsSize' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='StatsXL' Content='XL' GroupName='StatsSize' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
      </StackPanel>
    </Border>
    </StackPanel>

    <StackPanel x:Name='PageRoutines' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Sleep timer' Style='{StaticResource Heading}'/>
        <TextBlock Text='Fades the music out over the last minute, pauses it and puts the volume back. Starts right away, without Save.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,10'>
          <Button x:Name='SleepCancel' Content='Cancel' Style='{StaticResource Quiet}' DockPanel.Dock='Right'/>
          <TextBlock x:Name='SleepStatus' Style='{StaticResource Label}' VerticalAlignment='Center'/>
        </DockPanel>
        <StackPanel Orientation='Horizontal'>
          <Button x:Name='Sleep15' Content='15 min' Style='{StaticResource Secondary}' Margin='0,0,6,0'/>
          <Button x:Name='Sleep30' Content='30 min' Style='{StaticResource Secondary}' Margin='0,0,6,0'/>
          <Button x:Name='Sleep60' Content='1 hour' Style='{StaticResource Secondary}' Margin='0,0,6,0'/>
          <Button x:Name='Sleep120' Content='2 hours' Style='{StaticResource Secondary}'/>
        </StackPanel>
        <StackPanel x:Name='SleepCase' Visibility='Collapsed' Margin='0,14,0,0'>
          <DockPanel Margin='0,0,0,14'>
            <TextBlock Text='Then turn off the case lights' Style='{StaticResource Label}'/>
            <ToggleButton x:Name='SleepLightsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
          </DockPanel>
          <DockPanel>
            <TextBlock Text='Then turn off the case screens' Style='{StaticResource Label}'/>
            <ToggleButton x:Name='SleepScreensToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
          </DockPanel>
        </StackPanel>
      </StackPanel>
    </Border>

    <Border x:Name='NightPanel' Style='{StaticResource Panel}' Visibility='Collapsed'>
      <StackPanel>
        <TextBlock Text='Night mode' Style='{StaticResource Heading}'/>
        <TextBlock Text='Turns the case off every night and back on in the morning. Anything you switch back on during the night stays on.' Style='{StaticResource Hint}' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Every night' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='NightToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Off at' Style='{StaticResource Label}' Width='150'/>
          <ComboBox x:Name='NightStartPick' Style='{StaticResource Pick}'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Back on at' Style='{StaticResource Label}' Width='150'/>
          <ComboBox x:Name='NightEndPick' Style='{StaticResource Pick}'/>
        </DockPanel>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='RGB lighting' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='NightLightsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel>
          <TextBlock Text='Screens' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='NightScreensToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
      </StackPanel>
    </Border>

    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Keyboard shortcuts' Style='{StaticResource Heading}'/>
        <TextBlock Text='Work from any app. Click one, then press the keys with Ctrl, Alt, Shift or Win. Backspace clears it, Esc keeps it.' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,4,0,14'/>
        <StackPanel x:Name='HotkeyRows'/>
      </StackPanel>
    </Border>
    </StackPanel>

    <StackPanel x:Name='PageGaming' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Game mode' Style='{StaticResource Heading}'/>
        <TextBlock Text='While a full-screen game runs, the lyrics screen shows the time, GPU temperature and load, VRAM and CPU load, with the song and current lyric along the bottom. Full-screen video from the app playing music does not count.' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Game HUD' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='GameToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <StackPanel x:Name='GameCase' Visibility='Collapsed'>
          <DockPanel Margin='0,0,0,10'>
            <TextBlock Text='Game lighting' Style='{StaticResource Label}'/>
            <ToggleButton x:Name='GameLightsToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
          </DockPanel>
          <StackPanel x:Name='GameColors' Orientation='Horizontal' Margin='0,0,0,6'/>
          <TextBlock Text='The case lights switch to this colour in game mode and go back afterwards.' Style='{StaticResource Hint}'/>
        </StackPanel>
      </StackPanel>
    </Border>

    <Border x:Name='HeatPanel' Style='{StaticResource Panel}' Visibility='Collapsed'>
      <StackPanel>
        <TextBlock Text='Heat alert' Style='{StaticResource Heading}'/>
        <TextBlock x:Name='HeatHint' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,4,0,14'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Pulse the case lights red' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='HeatToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel LastChildFill='False'>
          <TextBlock Text='GPU limit' Style='{StaticResource Label}' DockPanel.Dock='Left'/>
          <Border Style='{StaticResource Segments}' DockPanel.Dock='Right'>
            <StackPanel Orientation='Horizontal'>
              <RadioButton x:Name='Heat75' Content='75 °C' GroupName='Heat' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='Heat80' Content='80 °C' GroupName='Heat' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='Heat85' Content='85 °C' GroupName='Heat' Style='{StaticResource Segment}'/>
              <RadioButton x:Name='Heat90' Content='90 °C' GroupName='Heat' Style='{StaticResource Segment}'/>
            </StackPanel>
          </Border>
        </DockPanel>
      </StackPanel>
    </Border>
    </StackPanel>

    <StackPanel x:Name='PageGeneral' Visibility='Collapsed'>
    <Border Style='{StaticResource Panel}'>
      <StackPanel>
        <TextBlock Text='Behaviour' Style='{StaticResource Heading}' Margin='0,0,0,14'/>
        <DockPanel Margin='0,0,0,4'>
          <TextBlock Text='Keep the mouse off the lyrics screen' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='MouseToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <TextBlock Text='Turn it off to use the play, seek and volume controls by hovering over the lyrics screen, and to click a lyric to jump to it.' Style='{StaticResource Hint}' Margin='0,0,0,14'/>
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
    </Grid>
    </ScrollViewer>
  </DockPanel>
</Border>";

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly Settings saved;
        readonly Settings draft;
        readonly NowPlayingWatcher nowPlaying = new NowPlayingWatcher();
        TrackInfo nudgeTrack;
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
            Height = Math.Min(820, SystemParameters.WorkArea.Height - 48);
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

            Choice("Highlight", new[] { "Lines", "Words" }, draft.Highlight, value => draft.Highlight = value);
            Find<Button>("NudgeEarlier").Click += (s, e) => Nudge(0.1);
            Find<Button>("NudgeLater").Click += (s, e) => Nudge(-0.1);
            nowPlaying.TrackChanged += next => { nudgeTrack = next; ShowNudge(); };
            Loaded += async (s, e) =>
            {
                ShowNudge();
                await nowPlaying.StartAsync();
                var poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                poll.Tick += (a, b) =>
                {
                    ShowSleep();
                    if (nowPlaying.Poll().Track != null || nudgeTrack == null) return;
                    nudgeTrack = null;
                    ShowNudge();
                };
                poll.Start();
            };
            Choice("Romanize", new[] { "Off", "Below", "Only" }, draft.RomanizeMode, value => draft.RomanizeMode = value);
            Toggle("StatsToggle", draft.ShowStats, value => draft.ShowStats = value);
            Choice("Stats", new[] { "S", "M", "L", "XL" }, draft.StatsSize, value => draft.StatsSize = value);
            Pick("DisplayPick", new[] { new KeyValuePair<string, string>("", "Automatic") }.Concat(System.Windows.Forms.Screen.AllScreens.Where(s => !s.Primary)
                .Select(s => new KeyValuePair<string, string>(s.DeviceName, s.DeviceName.Replace(@"\\.\", "") + " · " + s.Bounds.Width + " × " + s.Bounds.Height))), draft.Display, value => draft.Display = value);
            Pick("FontPick", new[] { new KeyValuePair<string, string>("", "Default") }.Concat(Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(f => f)
                .Select(f => new KeyValuePair<string, string>(f, f))), draft.LyricFont, value => draft.LyricFont = value);
            Choice("Size", new[] { "S", "M", "L", "XL" }, draft.LyricSize, value => draft.LyricSize = value);
            Choice("Align", new[] { "Left", "Center" }, draft.LyricAlign, value => draft.LyricAlign = value);
            Choice("Color", new[] { "White", "Cover" }, draft.HighlightColor, value => draft.HighlightColor = value);
            Choice("Background", new[] { "Cover", "Idle" }, draft.BackgroundStyle, value => draft.BackgroundStyle = value);
            Toggle("CoverToggle", draft.ShowCover, value => draft.ShowCover = value);
            Toggle("VisualizerToggle", draft.MusicVisualizer, value => draft.MusicVisualizer = value);

            foreach (var minutes in new[] { 15, 30, 60, 120 })
                Find<Button>("Sleep" + minutes).Click += (s, e) =>
                {
                    new SleepTimer { Until = DateTime.UtcNow.AddMinutes(minutes), Lights = caseAvailable && saved.SleepLights, Screens = caseAvailable && saved.SleepScreens }.Save();
                    ShowSleep();
                };
            Find<Button>("SleepCancel").Click += (s, e) => { SleepTimer.Cancel(); ShowSleep(); };
            Remembered("SleepLightsToggle", saved.SleepLights, value => saved.SleepLights = draft.SleepLights = value);
            Remembered("SleepScreensToggle", saved.SleepScreens, value => saved.SleepScreens = draft.SleepScreens = value);
            ShowSleep();
            var times = Enumerable.Range(0, 48).Select(half => TimeSpan.FromMinutes(30 * half).ToString(@"hh\:mm")).Select(time => new KeyValuePair<string, string>(time, time)).ToList();
            Toggle("NightToggle", draft.NightEnabled, value => draft.NightEnabled = value);
            Pick("NightStartPick", times, draft.NightStart, value => draft.NightStart = value);
            Pick("NightEndPick", times, draft.NightEnd, value => draft.NightEnd = value);
            Toggle("NightLightsToggle", draft.NightLights, value => draft.NightLights = value);
            Toggle("NightScreensToggle", draft.NightScreens, value => draft.NightScreens = value);
            Toggle("GameToggle", draft.GameMode, value => draft.GameMode = value);
            Toggle("GameLightsToggle", draft.GameLighting, value => { draft.GameLighting = value; ShowSwatches(); });
            foreach (var hex in new[] { "#FF2D2D", "#FF7A00", "#FFD000", "#00E65A", "#00D9FF", "#2D6BFF", "#9B3DFF", "#FF2DAA", "#FFFFFF" })
            {
                var swatch = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 0, 10, 0), BorderThickness = new Thickness(3), Cursor = Cursors.Hand, Tag = hex, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) };
                swatch.MouseLeftButtonUp += (s, e) => { draft.GameColor = hex; ShowSwatches(); Changed(); };
                Find<StackPanel>("GameColors").Children.Add(swatch);
            }
            ShowSwatches();
            Toggle("HeatToggle", draft.HeatAlert, value => draft.HeatAlert = value);
            Choice("Heat", new[] { "75", "80", "85", "90" }, draft.HeatLimit.ToString(), value => draft.HeatLimit = int.Parse(value));
            var gpu = new HardwareMonitor();
            Find<TextBlock>("HeatHint").Text = gpu.HasGpu
                ? "Watches your " + gpu.GpuName + ", now at " + gpu.GpuTemperature() + " °C. The lights pulse red from the limit until it cools 5 °C below it, then your lighting comes back."
                : "Needs an NVIDIA graphics card, and none was found.";
            Find<ToggleButton>("HeatToggle").IsEnabled = gpu.HasGpu;
            HotkeyRow("Lyrics earlier", () => draft.HotkeyEarlier, value => draft.HotkeyEarlier = value);
            HotkeyRow("Lyrics later", () => draft.HotkeyLater, value => draft.HotkeyLater = value);
            Toggle("MouseToggle", draft.KeepMouseOff, value => draft.KeepMouseOff = value);
            Toggle("TrayToggle", draft.ShowTrayIcon, value => draft.ShowTrayIcon = value);
            Toggle("StartupToggle", startWithWindows, value => startWithWindows = value);
            Choice("Idle", new[] { "5", "20", "60" }, draft.IdleAfterPauseSeconds.ToString(), value => draft.IdleAfterPauseSeconds = int.Parse(value));

            CaseSwitch("LightsToggle", off => CaseSwitches.Load().SetLightsOffAsync(off, saved.LightsExcluded));
            CaseSwitch("ScreensToggle", off => CaseSwitches.Load().SetScreensOffAsync(off, saved.ScreensExcluded));
            ShowSwitches();
            Settings.WatchDataFile(CaseSwitches.FileName, Dispatcher, ShowSwitches);
            Loaded += async (s, e) =>
            {
                if (!await LConnect.Available()) return;
                caseAvailable = true;
                Find<StackPanel>("SleepCase").Visibility = Find<Border>("NightPanel").Visibility = Find<StackPanel>("GameCase").Visibility = Find<Border>("HeatPanel").Visibility = Visibility.Visible;
                HotkeyRow("RGB lighting on or off", () => draft.HotkeyLights, value => draft.HotkeyLights = value, 0);
                HotkeyRow("Screens on or off", () => draft.HotkeyScreens, value => draft.HotkeyScreens = value, 1);
                Find<RadioButton>("TabCase").Visibility = Visibility.Visible;
                if (!pageChosen) Find<RadioButton>("TabCase").IsChecked = true;
                try
                {
                    DeviceRows("LightDevices", await LConnect.LightingDevices(), () => draft.LightsExcluded, value => draft.LightsExcluded = value);
                    DeviceRows("ScreenDevices", await LConnect.ScreenDevices(), () => draft.ScreensExcluded, value => draft.ScreensExcluded = value);
                    Find<Border>("DevicesPanel").Visibility = Visibility.Visible;
                }
                catch (Exception failure) when (failure is HttpRequestException || failure is TaskCanceledException || failure is InvalidOperationException || failure is System.IO.IOException)
                {
                    Log.Write("case devices: " + failure.Message);
                }
            };

            foreach (var page in Pages)
            {
                var name = page;
                var tab = Find<RadioButton>("Tab" + name);
                tab.Checked += (s, e) => ShowPage(name);
                tab.PreviewMouseLeftButtonDown += (s, e) => pageChosen = true;
            }
            Find<RadioButton>("TabLyrics").IsChecked = true;

            Find<Button>("SaveButton").Click += (s, e) => Save();
            Find<Button>("CancelButton").Click += (s, e) => Close();
            Find<Button>("ExitButton").Click += (s, e) => { Program.StopAgent(); Close(); };

            ScreenPlacement.PreferredDisplay = saved.Display;
            var screen = ScreenPlacement.FindLyricsScreen();
            Find<TextBlock>("StatusText").Text = screen != null
                ? "Showing on " + screen.DeviceName.Replace(@"\\.\", "") + " (" + screen.Bounds.Width + " × " + screen.Bounds.Height + ")"
                : "No lyrics screen found. Pick one under Screen and look, or turn on Secondary screen for a Lian Li case screen in L-Connect 3.";
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

        void Remembered(string name, bool value, Action<bool> set)
        {
            var toggle = Find<ToggleButton>(name);
            toggle.IsChecked = value;
            RoutedEventHandler flipped = (s, e) =>
            {
                set(toggle.IsChecked == true);
                saved.Save();
                Changed();
            };
            toggle.Checked += flipped;
            toggle.Unchecked += flipped;
        }

        void ShowSwatches()
        {
            var swatches = Find<StackPanel>("GameColors");
            swatches.IsEnabled = draft.GameLighting;
            swatches.Opacity = draft.GameLighting ? 1 : 0.35;
            foreach (Border swatch in swatches.Children) swatch.BorderBrush = Graphite.Brush((string)swatch.Tag == draft.GameColor ? Graphite.Ink : Graphite.Surface);
        }

        void ShowSleep()
        {
            var timer = SleepTimer.Load();
            Find<TextBlock>("SleepStatus").Text = timer == null ? "Off" : "Stops at " + timer.Until.ToLocalTime().ToString("HH:mm");
            Find<Button>("SleepCancel").Visibility = timer == null ? Visibility.Collapsed : Visibility.Visible;
        }

        void HotkeyRow(string label, Func<string> current, Action<string> set, int? at = null)
        {
            var button = new Button { Style = (Style)root.FindResource("Secondary"), MinWidth = 150, Content = Hotkeys.Display(current()) };
            var note = new TextBlock { Style = (Style)root.FindResource("Hint"), Foreground = Graphite.Brush(Color.FromRgb(0xF2, 0xA0, 0x9A)), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
            var capturing = false;
            button.Click += (s, e) =>
            {
                capturing = true;
                button.Content = "Press keys…";
            };
            button.LostKeyboardFocus += (s, e) =>
            {
                capturing = false;
                button.Content = Hotkeys.Display(current());
            };
            button.PreviewKeyDown += (s, e) =>
            {
                if (!capturing) return;
                e.Handled = true;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (new[] { Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin }.Contains(key)) return;
                capturing = false;
                note.Visibility = Visibility.Collapsed;
                if (key == Key.Back)
                {
                    set("");
                    Changed();
                }
                else if (key != Key.Escape && Keyboard.Modifiers != ModifierKeys.None)
                {
                    var hotkey = Hotkeys.Format(Keyboard.Modifiers, key);
                    var ours = new[] { saved.HotkeyLights, saved.HotkeyScreens, saved.HotkeyEarlier, saved.HotkeyLater }.Contains(hotkey);
                    var handle = new WindowInteropHelper(this).Handle;
                    if (ours || Hotkeys.Register(handle, 99, hotkey))
                    {
                        if (!ours) Hotkeys.UnregisterHotKey(handle, 99);
                        set(hotkey);
                        Changed();
                    }
                    else
                    {
                        note.Text = Hotkeys.Display(hotkey) + " is already used by another app.";
                        note.Visibility = Visibility.Visible;
                    }
                }
                button.Content = Hotkeys.Display(current());
            };
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(button, Dock.Right);
            row.Children.Add(button);
            row.Children.Add(new TextBlock { Text = label, Style = (Style)root.FindResource("Label"), VerticalAlignment = VerticalAlignment.Center });
            var rows = Find<StackPanel>("HotkeyRows");
            var group = new StackPanel { Children = { row, note } };
            if (at.HasValue) rows.Children.Insert(at.Value, group); else rows.Children.Add(group);
        }

        bool caseAvailable, showingSwitches;

        void ShowSwitches()
        {
            var switches = CaseSwitches.Load();
            showingSwitches = true;
            Find<ToggleButton>("LightsToggle").IsChecked = !switches.LightsOff;
            Find<ToggleButton>("ScreensToggle").IsChecked = !switches.ScreensOff;
            showingSwitches = false;
        }

        void CaseSwitch(string name, Func<bool, Task> setOff)
        {
            var toggle = Find<ToggleButton>(name);
            var reverting = false;
            RoutedEventHandler flipped = async (s, e) =>
            {
                if (reverting || showingSwitches) return;
                var error = Find<TextBlock>("CaseError");
                toggle.IsEnabled = false;
                try
                {
                    await setOff(toggle.IsChecked != true);
                    error.Visibility = Visibility.Collapsed;
                }
                catch (Exception failure) when (failure is HttpRequestException || failure is TaskCanceledException || failure is InvalidOperationException || failure is System.IO.IOException)
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

        static readonly string[] Pages = { "Case", "Lyrics", "Look", "Idle", "Routines", "Gaming", "General" };
        bool pageChosen;

        public void Open(string page)
        {
            pageChosen = true;
            Find<RadioButton>("Tab" + page).Visibility = Visibility.Visible;
            Find<RadioButton>("Tab" + page).IsChecked = true;
        }

        void ShowPage(string name)
        {
            foreach (var page in Pages) Find<StackPanel>("Page" + page).Visibility = page == name ? Visibility.Visible : Visibility.Collapsed;
            Find<ScrollViewer>("Pages").ScrollToTop();
        }

        void Pick(string name, IEnumerable<KeyValuePair<string, string>> options, string current, Action<string> set)
        {
            var box = Find<ComboBox>(name);
            foreach (var option in options) box.Items.Add(new ComboBoxItem { Content = option.Value, Tag = option.Key });
            box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == current) ?? box.Items[0];
            box.SelectionChanged += (s, e) =>
            {
                set((string)((ComboBoxItem)box.SelectedItem).Tag);
                Changed();
            };
        }

        void DeviceRows(string host, List<KeyValuePair<string, string>> devices, Func<string> excluded, Action<string> setExcluded)
        {
            var rows = Find<StackPanel>(host);
            rows.Children.Clear();
            foreach (var device in devices)
            {
                var toggle = new ToggleButton { Style = (Style)root.FindResource("Switch"), HorizontalAlignment = HorizontalAlignment.Right, IsChecked = !CaseSwitches.Ids(excluded()).Contains(device.Key) };
                var id = device.Key;
                RoutedEventHandler flipped = (s, e) =>
                {
                    var ids = CaseSwitches.Ids(excluded());
                    if (toggle.IsChecked == true) ids.Remove(id); else ids.Add(id);
                    setExcluded(string.Join(",", ids));
                    Changed();
                };
                toggle.Checked += flipped;
                toggle.Unchecked += flipped;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                DockPanel.SetDock(toggle, Dock.Right);
                row.Children.Add(toggle);
                row.Children.Add(new TextBlock { Text = device.Value, Style = (Style)root.FindResource("Label") });
                rows.Children.Add(row);
            }
        }

        void Nudge(double step)
        {
            if (nudgeTrack == null) return;
            SyncNudges.Nudge(nudgeTrack.Key, step);
            ShowNudge();
        }

        void ShowNudge()
        {
            Find<TextBlock>("NudgeSong").Text = nudgeTrack == null ? "Nothing playing" : nudgeTrack.Title + " · " + nudgeTrack.Artist;
            var nudge = nudgeTrack != null && SyncNudges.Load().TryGetValue(nudgeTrack.Key, out var value) ? value : 0;
            Find<TextBlock>("NudgeValue").Text = nudge == 0 ? "in sync" : (nudge > 0 ? "+" : "") + nudge.ToString("0.0") + " s";
            Find<Button>("NudgeEarlier").IsEnabled = Find<Button>("NudgeLater").IsEnabled = nudgeTrack != null;
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