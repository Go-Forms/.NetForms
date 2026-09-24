namespace Gallery
{
    /// <summary>
    /// A form whose image and icon live in its .resx, as Visual Studio keeps them: read at run time through
    /// ComponentResourceManager, on Windows and Linux alike.
    /// </summary>
    public partial class ResourcesForm : Form
    {
        public ResourcesForm()
        {
            InitializeComponent();
        }
    }
}
