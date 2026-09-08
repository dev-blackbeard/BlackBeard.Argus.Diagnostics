using Microsoft.Maui.Controls;

// Lets a consuming page write xmlns="https://blackbeard.dev/argus/controls/maui" instead of the
// longer xmlns:argus="clr-namespace:Argus.Controls.Maui;assembly=Argus.Controls.Maui" form. Both
// resolve to the same types -- this is purely a nicer import, not a functional change -- and the
// URL is just a string key here, it does not need to resolve to anything real.
[assembly: XmlnsDefinition("https://blackbeard.dev/argus/controls/maui", "Argus.Controls.Maui")]
