// Expose the implicit Program type to test projects by providing a public partial
// Program class. This is a common pattern for integration tests using
// WebApplicationFactory<TEntryPoint> when top-level statements are used.
public partial class Program { }
