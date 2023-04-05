// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import '@microsoft/dotnet-js-interop';

export const domFunctions = {
  focus,
  focusOnNavigate
};

function focus(element: HTMLOrSVGElement, preventScroll: boolean): void {
  if (element instanceof HTMLElement) {
    element.focus({ preventScroll });
  } else if (element instanceof SVGElement) {
    if (element.hasAttribute('tabindex')) {
      element.focus({ preventScroll });
    } else {
      throw new Error('Unable to focus an SVG element that does not have a tabindex.');
    }
  } else {
    throw new Error('Unable to focus an invalid element.');
  }
}

function focusOnNavigate(selector: string): void {
  const preventScroll = location.hash.length > 1 && elementExists(location.hash.slice(1));
  focusBySelector(selector, preventScroll);
}

function focusBySelector(selector: string, preventScroll: boolean): void {
  const element = document.querySelector(selector) as HTMLElement;
  if (element) {
    // If no explicit tabindex is defined, mark it as programmatically-focusable.
    // This does actually add a new HTML attribute, but it shouldn't interfere with
    // diffing because diffing only deals with the attributes you have in your code.
    if (!element.hasAttribute('tabindex')) {
      element.tabIndex = -1;
    }

    element.focus({ preventScroll: preventScroll });
  }
}

function elementExists(identifier: string): boolean {
  const element = document.getElementById(identifier)
      || document.getElementsByName(identifier)[0];
  return !!element;
}