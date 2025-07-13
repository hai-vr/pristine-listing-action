using Hai.PristineListing.Core;
using Hai.PristineListing.Input;
using Shouldly;

namespace Hai.PristineListing.Tests;

public class InputParserTest
{
    private InputParser _sut;
    private PLCoreInput _minimalExpectedResult;

    [SetUp]
    public void SetUp()
    {
        _sut = new InputParser();
        
        _minimalExpectedResult = new PLCoreInput
        {
            listingData = new PLCoreInputListingData
            {
                name = "Name",
                author = "Author",
                url = "https://example.com/index.json",
                id = "com.example.listing"
            },
            settings = new PLCoreInputSettings
            {
                excessiveModeToleratesPackageJsonAssetMissing = true,
                includeDownloadCount = false
            },
            products =
            [
                new PLCoreInputProduct
                {
                    repository = "hai-vr/upm-test-package",
                    includePrereleases = true,
                    mode = PLCoreInputMode.PackageJsonAssetOnly,
                    onlyPackageNames = []
                }
            ]
        };
    }

    [Test]
    public void It_should_parse_minimal_input_json()
    {
        // When
        PLCoreInput result = _sut.Parse(
"""
{
    "listingData": {
        "name": "Name",
        "author": "Author",
        "url": "https://example.com/index.json",
        "id": "com.example.listing"
    },
    "settings": {},
    "products": [
        {
            "repository": "hai-vr/upm-test-package"
        }
    ]
}
""");
        
        // Then
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_parse_several_products()
    {
        // When
        PLCoreInput result = _sut.Parse(
            """
            {
                "listingData": {
                    "name": "Name",
                    "author": "Author",
                    "url": "https://example.com/index.json",
                    "id": "com.example.listing"
                },
                "settings": {},
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    },
                    {
                        "repository": "hai-vr/other-test-package"
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.products.Add(new PLCoreInputProduct
            {
                repository = "hai-vr/other-test-package",
                includePrereleases = true,
                mode = PLCoreInputMode.PackageJsonAssetOnly,
                onlyPackageNames = []
            }
        );
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }

    [Test]
    public void It_should_propagate_prerelease_false_setting_to_products_that_do_not_define_it()
    {
        // When
        PLCoreInput result = _sut.Parse(
            """
            {
                "listingData": {
                    "name": "Name",
                    "author": "Author",
                    "url": "https://example.com/index.json",
                    "id": "com.example.listing"
                },
                "settings": {
                    "defaultIncludePrereleases": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.products[0].includePrereleases = false;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }

    [Test]
    public void It_should_propagate_prerelease_true_setting_to_products_that_do_not_define_it()
    {
        // When
        PLCoreInput result = _sut.Parse(
            """
            {
                "listingData": {
                    "name": "Name",
                    "author": "Author",
                    "url": "https://example.com/index.json",
                    "id": "com.example.listing"
                },
                "settings": {
                    "defaultIncludePrereleases": true
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.products[0].includePrereleases = true;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }

    [Test]
    public void It_should_NOT_propagate_prerelease_false_setting_to_products_that_DO_define_it()
    {
        // When
        PLCoreInput result = _sut.Parse(
            """
            {
                "listingData": {
                    "name": "Name",
                    "author": "Author",
                    "url": "https://example.com/index.json",
                    "id": "com.example.listing"
                },
                "settings": {
                    "defaultIncludePrereleases": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "includePrereleases": true
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.products[0].includePrereleases = true;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }

    [Test]
    public void It_should_NOT_propagate_prerelease_true_setting_to_products_that_DO_define_it()
    {
        // When
        PLCoreInput result = _sut.Parse(
            """
            {
                "listingData": {
                    "name": "Name",
                    "author": "Author",
                    "url": "https://example.com/index.json",
                    "id": "com.example.listing"
                },
                "settings": {
                    "defaultIncludePrereleases": true
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "includePrereleases": false
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.products[0].includePrereleases = false;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    // TODO:
    // Test for defaultMode / mode
    // Test for excessiveModeToleratesPackageJsonAssetMissing
    // Test for includeDownloadCount
    // Test for onlyPackageNames
}