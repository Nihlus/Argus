//
//  WebDriverExtensions.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2021 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Affero General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Affero General Public License for more details.
//
//  You should have received a copy of the GNU Affero General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using OpenQA.Selenium;

namespace Argus.Collector.FList.Extensions;

/// <summary>
/// Defines extension methods for the <see cref="IWebDriver"/> interface.
/// </summary>
public static class WebDriverExtensions
{
    /// <summary>
    /// Attempts to find an element in the current page using the given selector.
    /// </summary>
    /// <param name="driver">The driver.</param>
    /// <param name="by">The selector.</param>
    /// <returns>The element, or null.</returns>
    public static IWebElement? FindElementSafe(this IWebDriver driver, By by)
    {
        try
        {
            return driver.FindElement(by);
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }
}
