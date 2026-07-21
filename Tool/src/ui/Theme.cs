using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace PackStudio
{
    /// <summary>
    /// The app's single (dark) theme. Every color, font size and control
    /// style lives here (PACKTOOL_DESIGN.md "Visual theme"). Warm charcoal
    /// surfaces (never pure black), off-white parchment text (never pure
    /// white), muted gold accents. Control templates are built via
    /// XamlReader at startup, no build-time XAML involved.
    /// </summary>
    internal static class Theme
    {
        // ── palette ─────────────────────────────────────────────────
        internal static readonly Color WindowBgC = C("#201C16");
        internal static readonly Color SurfaceC = C("#2A251D");
        internal static readonly Color SurfaceAltC = C("#332C22");
        internal static readonly Color InputBgC = C("#1B1813");
        internal static readonly Color BorderC = C("#4A4133");
        internal static readonly Color TextC = C("#EAE3D2");
        internal static readonly Color TextDimC = C("#A69C86");
        internal static readonly Color AccentC = C("#C9A86A");
        internal static readonly Color SelectedC = C("#3F372A");

        internal static readonly Brush WindowBg = B(WindowBgC);
        internal static readonly Brush Surface = B(SurfaceC);
        internal static readonly Brush SurfaceAlt = B(SurfaceAltC);
        internal static readonly Brush InputBg = B(InputBgC);
        internal static readonly Brush BorderBrush = B(BorderC);
        internal static readonly Brush Text = B(TextC);
        internal static readonly Brush TextDim = B(TextDimC);
        internal static readonly Brush Accent = B(AccentC);
        internal static readonly Brush Selected = B(SelectedC);

        // severity + chat-channel colors (game log colors, softened for dark)
        internal static readonly Brush Blocker = B(C("#E07A6A"));
        internal static readonly Brush Warning = B(C("#D9A94E"));
        internal static readonly Brush Tip = B(C("#86A9C6"));
        internal static readonly Brush Ok = B(C("#8FBF7F"));
        internal static readonly Brush ChatShout = B(C("#E5993F"));
        internal static readonly Brush ChatGuild = B(C("#8CC08C"));
        internal static readonly Brush ChatWhisper = B(C("#D98CC7"));

        internal const double FontSize = 13.5;

        private static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }

        private static Brush B(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        // ── application-wide implicit styles ───────────────────────

        internal static void Apply(Application app)
        {
            app.Resources[typeof(TextBlock)] = TextBlockStyle();
            app.Resources[typeof(Button)] = ButtonStyle();
            app.Resources[typeof(TextBox)] = TextBoxStyle();
            app.Resources[typeof(CheckBox)] = CheckBoxStyle();
            app.Resources[typeof(ComboBox)] = ComboBoxStyle();
            app.Resources[typeof(ComboBoxItem)] = ItemStyle(typeof(ComboBoxItem));
            app.Resources[typeof(ListBox)] = ListBoxStyle();
            app.Resources[typeof(ListBoxItem)] = ItemStyle(typeof(ListBoxItem));
            app.Resources[typeof(ToolTip)] = ToolTipStyle();
        }

        private static Style TextBlockStyle()
        {
            Style style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Text));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, FontSize));
            return style;
        }

        private static Style ToolTipStyle()
        {
            Style style = new Style(typeof(ToolTip));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceAlt));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize - 0.5));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
            return style;
        }

        private static Style ListBoxStyle()
        {
            Style style = new Style(typeof(ListBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            return style;
        }

        private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
            + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

        private static ControlTemplate Template(string xaml)
        {
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        private static Style ButtonStyle()
        {
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceAlt));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 3, 10, 3)));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, Template(
                "<ControlTemplate TargetType='Button' " + Ns + ">"
                + "<Border x:Name='bd' Background='{TemplateBinding Background}'"
                + " BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='3'"
                + " Padding='{TemplateBinding Padding}'>"
                + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>"
                + "<ControlTemplate.Triggers>"
                + "<Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='bd' Property='Background' Value='#3F372A'/>"
                + "<Setter TargetName='bd' Property='BorderBrush' Value='#C9A86A'/></Trigger>"
                + "<Trigger Property='IsPressed' Value='True'>"
                + "<Setter TargetName='bd' Property='Background' Value='#4A4133'/></Trigger>"
                + "<Trigger Property='IsEnabled' Value='False'>"
                + "<Setter Property='Opacity' Value='0.45'/></Trigger>"
                + "</ControlTemplate.Triggers></ControlTemplate>")));
            return style;
        }

        private static Style TextBoxStyle()
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, InputBg));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            style.Setters.Add(new Setter(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, Accent));
            style.Setters.Add(new Setter(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, Accent));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            style.Setters.Add(new Setter(Control.TemplateProperty, Template(
                "<ControlTemplate TargetType='TextBox' " + Ns + ">"
                + "<Border x:Name='bd' Background='{TemplateBinding Background}'"
                + " BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='3'>"
                + "<ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></Border>"
                + "<ControlTemplate.Triggers>"
                + "<Trigger Property='IsKeyboardFocusWithin' Value='True'>"
                + "<Setter TargetName='bd' Property='BorderBrush' Value='#C9A86A'/></Trigger>"
                + "</ControlTemplate.Triggers></ControlTemplate>")));
            return style;
        }

        private static Style CheckBoxStyle()
        {
            Style style = new Style(typeof(CheckBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.TemplateProperty, Template(
                "<ControlTemplate TargetType='CheckBox' " + Ns + ">"
                + "<StackPanel Orientation='Horizontal' Background='Transparent'>"
                + "<Border x:Name='box' Width='16' Height='16' Background='#1B1813' BorderBrush='#4A4133'"
                + " BorderThickness='1' CornerRadius='2' VerticalAlignment='Center'>"
                + "<Path x:Name='mark' Data='M 3,8 L 6.5,11.5 L 13,4' Stroke='#C9A86A' StrokeThickness='2'"
                + " Visibility='Collapsed'/></Border>"
                + "<ContentPresenter Margin='6,0,0,0' VerticalAlignment='Center' RecognizesAccessKey='True'/>"
                + "</StackPanel><ControlTemplate.Triggers>"
                + "<Trigger Property='IsChecked' Value='True'>"
                + "<Setter TargetName='mark' Property='Visibility' Value='Visible'/></Trigger>"
                + "<Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='box' Property='BorderBrush' Value='#C9A86A'/></Trigger>"
                + "</ControlTemplate.Triggers></ControlTemplate>")));
            return style;
        }

        private static Style ComboBoxStyle()
        {
            Style style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, InputBg));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrush));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            style.Setters.Add(new Setter(Control.TemplateProperty, Template(
                "<ControlTemplate TargetType='ComboBox' " + Ns + ">"
                + "<Grid>"
                + "<ToggleButton x:Name='toggle' Focusable='False' ClickMode='Press'"
                + " IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>"
                + "<ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>"
                + "<Border x:Name='tb' Background='#1B1813' BorderBrush='#4A4133' BorderThickness='1' CornerRadius='3'>"
                + "<Path HorizontalAlignment='Right' Margin='0,0,8,0' VerticalAlignment='Center'"
                + " Data='M 0,0 L 4,4 L 8,0' Stroke='#A69C86' StrokeThickness='1.5'/></Border>"
                + "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='tb' Property='BorderBrush' Value='#C9A86A'/></Trigger>"
                + "</ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton>"
                + "<ContentPresenter Content='{TemplateBinding SelectionBoxItem}'"
                + " ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'"
                + " Margin='7,3,24,3' VerticalAlignment='Center' IsHitTestVisible='False'/>"
                + "<Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom'"
                + " AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>"
                + "<Border Background='#2A251D' BorderBrush='#4A4133' BorderThickness='1' CornerRadius='3'"
                + " MinWidth='{TemplateBinding ActualWidth}' MaxHeight='320'>"
                + "<ScrollViewer><ItemsPresenter/></ScrollViewer></Border></Popup>"
                + "</Grid></ControlTemplate>")));
            return style;
        }

        private static Style ItemStyle(Type itemType)
        {
            Style style = new Style(itemType);
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.FontSizeProperty, FontSize));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 3, 8, 3)));
            string tag = itemType.Name; // ComboBoxItem / ListBoxItem
            style.Setters.Add(new Setter(Control.TemplateProperty, Template(
                "<ControlTemplate TargetType='" + tag + "' " + Ns + ">"
                + "<Border x:Name='bd' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='2'>"
                + "<ContentPresenter/></Border>"
                + "<ControlTemplate.Triggers>"
                + "<Trigger Property='IsMouseOver' Value='True'>"
                + "<Setter TargetName='bd' Property='Background' Value='#3F372A'/></Trigger>"
                + "<Trigger Property='IsSelected' Value='True'>"
                + "<Setter TargetName='bd' Property='Background' Value='#4A4133'/></Trigger>"
                + "</ControlTemplate.Triggers></ControlTemplate>")));
            return style;
        }

        // ── small factories ─────────────────────────────────────────

        internal static TextBlock Title(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = FontSize + 6,
                FontWeight = FontWeights.Bold,
                Foreground = Accent,
            };
        }

        internal static TextBlock SectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = FontSize + 1.5,
                FontWeight = FontWeights.Bold,
                Foreground = Text,
            };
        }

        internal static TextBlock Body(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        }

        internal static TextBlock Dim(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = TextDim,
                FontSize = FontSize - 1,
            };
        }

        internal static Border Panel(UIElement child)
        {
            return new Border
            {
                Background = Surface,
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                Child = child,
            };
        }
    }
}
