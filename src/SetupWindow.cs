using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;

namespace GraphiteLyrics
{
    sealed class SetupWindow : Window
    {
        const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Background='#0C0C0E' Padding='28,24,28,24' TextElement.FontFamily='Segoe UI Variable Text, Segoe UI' TextElement.Foreground='#E8E8EA'>
" + GraphiteXaml.Resources + @"
  <StackPanel>
    <StackPanel Orientation='Horizontal'>
      <Border Width='48' Height='48' CornerRadius='12' Background='#131316' BorderBrush='#26262C' BorderThickness='1' Margin='0,0,16,0'>
        <StackPanel VerticalAlignment='Center' Margin='11,0,0,0'>
          <Border Width='22' Height='4' CornerRadius='2' Background='#8B8B95' HorizontalAlignment='Left'/>
          <StackPanel Orientation='Horizontal' Margin='0,4,0,4'>
            <Border Width='18' Height='5' CornerRadius='2.5' Background='#E8E8EA'/>
            <Ellipse Width='4' Height='4' Fill='#46B97A' Margin='3,0,0,0' VerticalAlignment='Center'/>
          </StackPanel>
          <Border Width='15' Height='4' CornerRadius='2' Background='#8B8B95' HorizontalAlignment='Left'/>
        </StackPanel>
      </Border>
      <StackPanel VerticalAlignment='Center'>
        <TextBlock x:Name='Heading' FontSize='24' FontWeight='SemiBold'/>
        <TextBlock x:Name='Subheading' Style='{StaticResource Hint}' Margin='0,2,0,0'/>
      </StackPanel>
    </StackPanel>

    <Border Style='{StaticResource Panel}' Margin='0,20,0,14'>
      <StackPanel>
        <TextBlock Style='{StaticResource Hint}' Margin='0,0,0,16' Text='Synced, animated lyrics for the Lian Li 9.2-inch case screen. It follows whatever Windows is playing and shows an idle loop when nothing is.'/>
        <TextBlock Text='Location' Style='{StaticResource Label}'/>
        <TextBlock x:Name='Location' FontFamily='Cascadia Mono, Consolas' FontSize='12' Foreground='#8B8B95' TextWrapping='Wrap' Margin='0,4,0,16'/>
        <DockPanel Margin='0,0,0,14'>
          <TextBlock Text='Start with Windows' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='StartupToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right'/>
        </DockPanel>
        <DockPanel>
          <TextBlock Text='Open Graphite Lyrics when done' Style='{StaticResource Label}'/>
          <ToggleButton x:Name='OpenToggle' Style='{StaticResource Switch}' HorizontalAlignment='Right' IsChecked='True'/>
        </DockPanel>
      </StackPanel>
    </Border>

    <DockPanel LastChildFill='False'>
      <Button x:Name='InstallButton' Style='{StaticResource Primary}' DockPanel.Dock='Right'/>
      <Button x:Name='CancelButton' Content='Cancel' Style='{StaticResource Quiet}' DockPanel.Dock='Right' Margin='0,0,8,0'/>
    </DockPanel>
  </StackPanel>
</Border>";

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        readonly FrameworkElement root;

        public SetupWindow()
        {
            Title = "Graphite Lyrics Setup";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Graphite.Brush(Graphite.Paper);
            root = (FrameworkElement)XamlReader.Parse(Xaml);
            Content = root;

            var installed = Installer.InstalledVersion;
            Find<TextBlock>("Heading").Text = installed == null ? "Install Graphite Lyrics" : "Update Graphite Lyrics";
            Find<TextBlock>("Subheading").Text = installed == null ? "Version " + Installer.Version
                : installed == "unknown" ? "Updating to version " + Installer.Version
                : "Version " + installed + " → " + Installer.Version;
            Find<TextBlock>("Location").Text = Installer.InstallDir;
            Find<ToggleButton>("StartupToggle").IsChecked = installed == null || StartupShortcut.Exists;
            var install = Find<Button>("InstallButton");
            install.Content = installed == null ? "Install" : "Update";
            install.Click += async (s, e) => await InstallAsync();
            Find<Button>("CancelButton").Click += (s, e) => Close();
            SourceInitialized += (s, e) =>
            {
                var dark = 1;
                DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
            };
        }

        T Find<T>(string name) where T : class => (T)root.FindName(name);

        async Task InstallAsync()
        {
            var startup = Find<ToggleButton>("StartupToggle").IsChecked == true;
            var open = Find<ToggleButton>("OpenToggle").IsChecked == true;
            var install = Find<Button>("InstallButton");
            install.IsEnabled = false;
            Find<Button>("CancelButton").IsEnabled = false;
            install.Content = "Installing…";
            try
            {
                await Task.Run(() => Installer.Install(startup));
            }
            catch (Exception error) when (error is UnauthorizedAccessException || error is System.IO.IOException)
            {
                Log.Write("install: " + error);
                Find<TextBlock>("Subheading").Text = "Couldn't install: " + error.Message;
                install.Content = "Try again";
                install.IsEnabled = true;
                Find<Button>("CancelButton").IsEnabled = true;
                return;
            }
            if (open) Process.Start(new ProcessStartInfo(Installer.InstalledExe) { UseShellExecute = false });
            Close();
        }
    }
}
