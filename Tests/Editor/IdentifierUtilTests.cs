// =============================================================================
// Bindery — NUnit tests for IdentifierUtil.ToIdentifier and IdentifierUtil.Dedupe.
// Run via Window ▸ General ▸ Test Runner (EditMode tab).
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;

namespace Bindery.Tests
{
    internal sealed class IdentifierUtilTests
    {
        // -------------------------------------------------------------------------
        // ToIdentifier — basic output cases
        // -------------------------------------------------------------------------

        [Test]
        public void ToIdentifier_NullOrEmpty_ReturnsFallback()
        {
            Assert.That(IdentifierUtil.ToIdentifier(null), Is.EqualTo("_"));
            Assert.That(IdentifierUtil.ToIdentifier(""), Is.EqualTo("_"));
        }

        [Test]
        public void ToIdentifier_AllIllegalChars_ReturnsFallback()
        {
            // spaces, hyphens, dots — all illegal; nothing left after stripping
            Assert.That(IdentifierUtil.ToIdentifier("- -"), Is.EqualTo("_"));
            Assert.That(IdentifierUtil.ToIdentifier("..."), Is.EqualTo("_"));
        }

        [Test]
        public void ToIdentifier_PlainAlpha_PreservesCase()
        {
            Assert.That(IdentifierUtil.ToIdentifier("Footer"), Is.EqualTo("Footer"));
            Assert.That(IdentifierUtil.ToIdentifier("okButton"), Is.EqualTo("okButton"));
            Assert.That(IdentifierUtil.ToIdentifier("LABEL"), Is.EqualTo("LABEL"));
        }

        [Test]
        public void ToIdentifier_UnderscoresPreserved()
        {
            Assert.That(IdentifierUtil.ToIdentifier("_private"), Is.EqualTo("_private"));
            Assert.That(IdentifierUtil.ToIdentifier("my_field"), Is.EqualTo("my_field"));
        }

        [Test]
        public void ToIdentifier_LeadingDigit_GetsPrefixUnderscore()
        {
            Assert.That(IdentifierUtil.ToIdentifier("3D"), Is.EqualTo("_3D"));
            Assert.That(IdentifierUtil.ToIdentifier("1stPanel"), Is.EqualTo("_1stPanel"));
        }

        [Test]
        public void ToIdentifier_ReservedKeyword_GetsAtPrefix()
        {
            Assert.That(IdentifierUtil.ToIdentifier("class"), Is.EqualTo("@class"));
            Assert.That(IdentifierUtil.ToIdentifier("new"), Is.EqualTo("@new"));
            Assert.That(IdentifierUtil.ToIdentifier("string"), Is.EqualTo("@string"));
            Assert.That(IdentifierUtil.ToIdentifier("int"), Is.EqualTo("@int"));
            Assert.That(IdentifierUtil.ToIdentifier("for"), Is.EqualTo("@for"));
        }

        [Test]
        public void ToIdentifier_IllegalCharsStripped_RestPreserved()
        {
            // spaces, hyphens and dots removed; alphanumeric/underscore kept
            Assert.That(IdentifierUtil.ToIdentifier("Submit Button"), Is.EqualTo("SubmitButton"));
            Assert.That(IdentifierUtil.ToIdentifier("ok-button"), Is.EqualTo("okbutton"));
            Assert.That(IdentifierUtil.ToIdentifier("Panel.Header"), Is.EqualTo("PanelHeader"));
        }

        [Test]
        public void ToIdentifier_MixedLegalAndIllegal_CasePreserved()
        {
            // All legal chars are kept in their original case
            Assert.That(IdentifierUtil.ToIdentifier("MyPanel!"), Is.EqualTo("MyPanel"));
            Assert.That(IdentifierUtil.ToIdentifier("ABC123_xyz"), Is.EqualTo("ABC123_xyz"));
        }

        [Test]
        public void ToIdentifier_KeywordAfterStripping_GetsAtPrefix()
        {
            // "for!" — after stripping '!' we have "for", which is a keyword
            Assert.That(IdentifierUtil.ToIdentifier("for!"), Is.EqualTo("@for"));
        }

        [Test]
        public void ToIdentifier_DigitOnlyName_GetsPrefixUnderscore()
        {
            Assert.That(IdentifierUtil.ToIdentifier("42"), Is.EqualTo("_42"));
        }

        // -------------------------------------------------------------------------
        // Dedupe
        // -------------------------------------------------------------------------

        [Test]
        public void Dedupe_UniqueInputs_ReturnedUnchanged()
        {
            var ids = new List<string> { "Button", "Label", "Image" };
            var result = IdentifierUtil.Dedupe(ids);
            Assert.That(result, Is.EqualTo(new[] { "Button", "Label", "Image" }));
        }

        [Test]
        public void Dedupe_FirstWins_DuplicateGetsSuffix()
        {
            var ids = new List<string> { "Button", "Button", "Button" };
            var result = IdentifierUtil.Dedupe(ids);
            Assert.That(result[0], Is.EqualTo("Button"));
            Assert.That(result[1], Is.EqualTo("Button_2"));
            Assert.That(result[2], Is.EqualTo("Button_3"));
        }

        [Test]
        public void Dedupe_ReservedPreClaimed_IdRenamedOnFirstOccurrence()
        {
            // "name" is in the reserved set; the first occurrence should be renamed too
            var ids = new List<string> { "name" };
            var result = IdentifierUtil.Dedupe(ids, reserved: new[] { "name" });
            Assert.That(result[0], Is.EqualTo("name_2"));
        }

        [Test]
        public void Dedupe_ReservedDoesNotAffectUnrelatedIds()
        {
            var ids = new List<string> { "Footer", "Header" };
            var result = IdentifierUtil.Dedupe(ids, reserved: new[] { "name" });
            Assert.That(result, Is.EqualTo(new[] { "Footer", "Header" }));
        }

        [Test]
        public void Dedupe_ReservedAndDuplicate_SuffixesIncrementPastReserved()
        {
            // reserved contains "name" and "name_2"; first occurrence gets name_3
            var ids = new List<string> { "name" };
            var result = IdentifierUtil.Dedupe(ids, reserved: new[] { "name", "name_2" });
            Assert.That(result[0], Is.EqualTo("name_3"));
        }

        [Test]
        public void Dedupe_OnCollision_FiresForEachRename()
        {
            var collisions = new List<(string original, string renamed)>();
            var ids = new List<string> { "Btn", "Btn" };
            IdentifierUtil.Dedupe(ids, onCollision: (orig, rn) => collisions.Add((orig, rn)));

            Assert.That(collisions, Has.Count.EqualTo(1));
            Assert.That(collisions[0].original, Is.EqualTo("Btn"));
            Assert.That(collisions[0].renamed, Is.EqualTo("Btn_2"));
        }

        [Test]
        public void Dedupe_EmptyInput_ReturnsEmpty()
        {
            var result = IdentifierUtil.Dedupe(new List<string>());
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Dedupe_SuffixCollision_SkipsToNextFree()
        {
            // If "Btn_2" is itself in the input, the third collision should get "Btn_3"
            var ids = new List<string> { "Btn", "Btn_2", "Btn" };
            var result = IdentifierUtil.Dedupe(ids);
            Assert.That(result[0], Is.EqualTo("Btn"));
            Assert.That(result[1], Is.EqualTo("Btn_2"));
            Assert.That(result[2], Is.EqualTo("Btn_3"));
        }
    }
}
