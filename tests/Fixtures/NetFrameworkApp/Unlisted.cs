// Left on disk but not in the project (removed from it in Visual Studio): it does not compile and must
// not be picked up by the SDK's glob after the conversion.
namespace FrameworkApp { class Unlisted { int x = "not an int"; } }
