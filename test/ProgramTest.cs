using System.Text;
using Hai.PristineListing.Aggregator;
using Hai.PristineListing.Core;
using Hai.PristineListing.Gatherer;
using Hai.PristineListing.Input;
using Hai.PristineListing.Modifier;
using Hai.PristineListing.Outputter;
using Moq;

namespace Hai.PristineListing.Tests;

public class ProgramTest
{
    [Test]
    public async Task This_test_should_fail()
    {
        Assert.Fail();
        await Task.CompletedTask;
    }

    [Test]
    public async Task It_should_read_file_contents_and_execute()
    {
        // Given
        var fileContents = "{}"; // FIXME: We may need to be able to pass a IFileSystem abstraction using System.IO.Abstractions
        await File.WriteAllTextAsync("test_input_file.json", fileContents, Encoding.UTF8);
        
        var parser = new Mock<InputParser>();
        var gatherer = new Mock<PLGatherer>(null);
        var aggregator = new Mock<PLAggregator>();
        var modifier = new Mock<PLModifier>(null);
        var outputter = new Mock<PLOutputter>(null);
        var sut = new Program(
            "test_input_file.json",
            parser.Object,
            gatherer.Object,
            aggregator.Object,
            modifier.Object,
            outputter.Object
        );
        var returnedFromParser = new PLCoreInput()
        {
            settings = new PLCoreInputSettings()
        };
        parser
            .Setup(it => it.Parse(fileContents))
            .Returns(returnedFromParser);
        var returnedFromGatherer = new PLCoreOutputListing();
        gatherer
            .Setup(it => it.DownloadAndAggregate(returnedFromParser))
            .ReturnsAsync(returnedFromGatherer);
        var returnedFromAggregator = new PLCoreAggregation();
        aggregator
            .Setup(it => it.DownloadAndAggregate(returnedFromParser))
            .ReturnsAsync(returnedFromAggregator);
        modifier
            .Setup(it => it.Modify(returnedFromParser, returnedFromGatherer));
        outputter
            .Setup(it => it.Write(returnedFromParser.settings, returnedFromGatherer));
        
        // When
        await sut.Run();
        
        // Then
        parser.Verify(it => it.Parse(fileContents), Times.Once);
        gatherer.Verify(it => it.DownloadAndAggregate(returnedFromParser), Times.Once);
        aggregator.Verify(it => it.DownloadAndAggregate(returnedFromParser), Times.Once);
        modifier.Verify(it => it.Modify(returnedFromParser, returnedFromGatherer), Times.Once);
        outputter.Verify(it => it.Write(returnedFromParser.settings, returnedFromGatherer), Times.Once);
    }
}