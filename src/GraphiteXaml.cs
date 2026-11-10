namespace GraphiteLyrics
{
    static class GraphiteXaml
    {
        public const string Resources = @"
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
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#FFFFFF'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Background' Value='#26262C'/><Setter Property='Foreground' Value='#8B8B95'/></Trigger>
        </ControlTemplate.Triggers>
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
  </Border.Resources>";
    }
}
