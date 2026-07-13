// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ThemeConverterTests
{
    using System.Collections.Generic;
    using FluentAssertions;
    using ThemeConverter;
    using Xunit;

    /// <summary>
    /// Tests to ensure that our internal data files don't have inconsistencies (duplicates, etc).
    /// </summary>
    public class DataValidationTest
    {
        [Fact]
        public void NoValidationError()
        {
            List<string> errors = [];

            Converter.ValidateDataFiles(errors.Add);

            errors.Should().BeEmpty();
        }
    }
}
