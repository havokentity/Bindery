// =============================================================================
// Bindery — NUnit tests for BinderySettings.Sanitize.
// Run via Window ▸ General ▸ Test Runner (EditMode tab).
// =============================================================================

using NUnit.Framework;

namespace Bindery.Tests
{
    internal sealed class SanitizeTests
    {
        // Sanitize keeps only chars legal inside a C# identifier (letters, digits,
        // underscore) and falls back to the given fallback when nothing remains.

        [Test]
        public void Sanitize_NullInput_ReturnsFallback()
        {
            Assert.That(BinderySettings.Sanitize(null, "View"), Is.EqualTo("View"));
        }

        [Test]
        public void Sanitize_EmptyInput_ReturnsFallback()
        {
            Assert.That(BinderySettings.Sanitize("", "View"), Is.EqualTo("View"));
        }

        [Test]
        public void Sanitize_AllIllegalChars_ReturnsFallback()
        {
            Assert.That(BinderySettings.Sanitize("!@#$%^&*()", "View"), Is.EqualTo("View"));
            Assert.That(BinderySettings.Sanitize("- -", "Fallback"), Is.EqualTo("Fallback"));
        }

        [Test]
        public void Sanitize_AlphanumericAndUnderscore_PassThrough()
        {
            Assert.That(BinderySettings.Sanitize("View", "X"), Is.EqualTo("View"));
            Assert.That(BinderySettings.Sanitize("MyView123", "X"), Is.EqualTo("MyView123"));
            Assert.That(BinderySettings.Sanitize("_View", "X"), Is.EqualTo("_View"));
        }

        [Test]
        public void Sanitize_IllegalCharsRemoved_LegalKept()
        {
            // spaces and dots stripped; letters/digits kept in original case
            Assert.That(BinderySettings.Sanitize("My View", "X"), Is.EqualTo("MyView"));
            Assert.That(BinderySettings.Sanitize("v1.0", "X"), Is.EqualTo("v10"));
            Assert.That(BinderySettings.Sanitize("Panel-Header", "X"), Is.EqualTo("PanelHeader"));
        }

        [Test]
        public void Sanitize_LeadingDigitNotEscaped()
        {
            // Sanitize does NOT prepend '_'; it is used for the suffix which is appended
            // to an already-valid name, so a leading digit is fine there.
            Assert.That(BinderySettings.Sanitize("2View", "X"), Is.EqualTo("2View"));
        }

        [Test]
        public void Sanitize_PreservesCase()
        {
            Assert.That(BinderySettings.Sanitize("UPPERCASE", "X"), Is.EqualTo("UPPERCASE"));
            Assert.That(BinderySettings.Sanitize("MixedCase", "X"), Is.EqualTo("MixedCase"));
        }

        [Test]
        public void Sanitize_FallbackUsedWhenNothingRemains()
        {
            Assert.That(BinderySettings.Sanitize("...", "DefaultSuffix"), Is.EqualTo("DefaultSuffix"));
        }

        [Test]
        public void Sanitize_FallbackNotUsedWhenSomethingRemains()
        {
            // Even one legal char is enough to NOT use the fallback
            Assert.That(BinderySettings.Sanitize("!V!", "X"), Is.EqualTo("V"));
        }
    }
}
