using Hai.PristineListing.Gatherer;
using Hai.PristineListing.Input;
using Hai.PristineListing.Modifier;
using Hai.PristineListing.Outputter;
using Moq;

namespace Hai.PristineListing.Tests;

public class ProgramTest
{
    [Test]
    public async Task It_should_parse_minimal_input_json()
    {
        // Given
        var parser = new Mock<InputParser>();
        var gatherer = new Mock<PLGatherer>(null);
        var modifier = new Mock<PLModifier>(null);
        var outputter = new Mock<PLOutputter>(null);
        var sut = new Program(
            "inputFile",
            parser.Object,
            gatherer.Object,
            modifier.Object,
            outputter.Object
        );
        
        // When
        // FIXME: We may need to be able to pass a IFileSystem abstraction using System.IO.Abstractions
        await sut.Run();
        
        // Then
        Assert.Fail(); // TODO
    }
}