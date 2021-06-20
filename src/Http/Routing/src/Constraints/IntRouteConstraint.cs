// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Routing.Constraints
{
    /// <summary>
    /// Constrains a route parameter to represent only 32-bit integer values.
    /// </summary>
    public class IntRouteConstraint : IRouteConstraint, ILiteralConstraint
    {
        /// <inheritdoc />
        public bool Match(
            HttpContext? httpContext,
            IRouter? route,
            string routeKey,
            RouteValueDictionary values,
            RouteDirection routeDirection)
        {
            if (routeKey == null)
            {
                throw new ArgumentNullException(nameof(routeKey));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.TryGetValue(routeKey, out var value) && value != null)
            {
                if (value is int)
                {
                    return true;
                }

                var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
                return valueString is not null && ((ILiteralConstraint)this).MatchLiteral(valueString);
            }

            return false;
        }

        bool ILiteralConstraint.MatchLiteral(string literal)
        {
            return int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
    }
}
