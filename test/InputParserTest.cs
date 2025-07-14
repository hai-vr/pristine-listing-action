using Hai.PristineListing.Core;
using Hai.PristineListing.Input;
using Shouldly;

namespace Hai.PristineListing.Tests;

[TestFixture]
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
                includeDownloadCount = false,
                forceOutputAuthorAsObject = false
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
            ],
            aggregateListings = []
        };
    }

#region Basics
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
#endregion
#region Prereleases
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
#endregion
#region Mode
    [Test]
    public void It_should_propagate_mode_PackageJsonAssetOnly_setting_to_products_that_do_not_define_it()
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
                    "defaultMode": "PackageJsonAssetOnly"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.PackageJsonAssetOnly;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_propagate_mode_ExcessiveWhenNeeded_setting_to_products_that_do_not_define_it()
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
                    "defaultMode": "ExcessiveWhenNeeded"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.ExcessiveWhenNeeded;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_propagate_mode_ExcessiveAlways_setting_to_products_that_do_not_define_it()
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
                    "defaultMode": "ExcessiveAlways"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.ExcessiveAlways;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_NOT_propagate_mode_PackageJsonAssetOnly_setting_to_products_that_DO_define_it()
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
                    "defaultMode": "PackageJsonAssetOnly"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "mode": "ExcessiveWhenNeeded"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.ExcessiveWhenNeeded;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_NOT_propagate_mode_ExcessiveWhenNeeded_setting_to_products_that_DO_define_it()
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
                    "defaultMode": "ExcessiveWhenNeeded"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "mode": "ExcessiveAlways"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.ExcessiveAlways;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_NOT_propagate_mode_ExcessiveAlways_setting_to_products_that_DO_define_it()
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
                    "defaultMode": "ExcessiveAlways"
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "mode": "ExcessiveWhenNeeded"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.products[0].mode = PLCoreInputMode.ExcessiveWhenNeeded;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
#endregion
#region Settings
    [Test]
    public void It_should_set_excessiveModeToleratesPackageJsonAssetMissing_to_false()
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
                    "excessiveModeToleratesPackageJsonAssetMissing": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
                
        // Then
        _minimalExpectedResult.settings.excessiveModeToleratesPackageJsonAssetMissing = false;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_set_excessiveModeToleratesPackageJsonAssetMissing_to_true()
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
                    "excessiveModeToleratesPackageJsonAssetMissing": true
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.settings.excessiveModeToleratesPackageJsonAssetMissing = true;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_set_includeDownloadCount_to_false()
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
                    "includeDownloadCount": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
                
        // Then
        _minimalExpectedResult.settings.includeDownloadCount = false;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_set_includeDownloadCount_to_true()
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
                    "includeDownloadCount": true
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.settings.includeDownloadCount = true;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    
    [Test]
    public void It_should_set_forceOutputAuthorAsObject_to_false()
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
                    "forceOutputAuthorAsObject": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
                
        // Then
        _minimalExpectedResult.settings.forceOutputAuthorAsObject = false;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_set_forceOutputAuthorAsObject_to_true()
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
                    "forceOutputAuthorAsObject": true
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package"
                    }
                ]
            }
            """);
            
        // Then
        _minimalExpectedResult.settings.forceOutputAuthorAsObject = true;
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
#endregion
#region Package names
    [Test]
    public void It_should_set_onlyPackageNames()
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
                    "includeDownloadCount": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "onlyPackageNames": [ "upm-test-package", "secondary-test-package" ]
                    }
                ]
            }
            """);
                    
        // Then
        _minimalExpectedResult.products[0].onlyPackageNames = ["upm-test-package", "secondary-test-package"];
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
    
    [Test]
    public void It_should_set_onlyPackageNames_to_empty_list()
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
                    "includeDownloadCount": false
                },
                "products": [
                    {
                        "repository": "hai-vr/upm-test-package",
                        "onlyPackageNames": []
                    }
                ]
            }
            """);
                    
        // Then
        _minimalExpectedResult.products[0].onlyPackageNames = [];
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
#endregion
#region Aggregate listings
    [Test]
    public void It_should_parse_minimal_aggregateListings()
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
                ],
                "aggregateListings": [
                    {
                        "listing": "https://example.com/other-listing.json"
                    }
                ]
            }
            """);
        
        // Then
        _minimalExpectedResult.aggregateListings.Add(new PLCoreAggregateListing
        {
            listing = "https://example.com/other-listing.json"
        });
        result.ShouldBeEquivalentTo(_minimalExpectedResult);
    }
#endregion

}