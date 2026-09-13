namespace SkrGui;
internal sealed partial class TextServicesImpl
{
    private enum SequenceStatus : byte { NotStarted, Started, StartedVirama, NearEnd }
public override bool IsValidIdentifier(Utf8StringView text)
{
    List<uint> codepoints = [];
    if (!TextAlgorithms.DecodeCodepoints(text, codepoints) || codepoints.Count == 0)
    {
        return false;
    }

    if (BackendValue == ETextServicesBackend.Fallback)
    {
        // The table-light profile is intentionally narrow: underscore or
        // XID_Start at the first scalar, then XID_Continue for the remainder.
        for (int i = 0; i < codepoints.Count; ++i)
        {
            int codepoint = (int)(codepoints[i]);
            if (!(i == 0u ?
                      codepoint == '_' ||
                          TextNative.u_hasBinaryProperty(codepoint, (int)IcuUProperty.UCHAR_XID_START) :
                      TextNative.u_hasBinaryProperty(codepoint, (int)IcuUProperty.UCHAR_XID_CONTINUE)))
            {
                return false;
            }
        }
        return true;
    }

    if (text.IsEmpty())
    {
        return false;
    }

    // Advanced identifiers must already be NFC. Rejecting non-normalized input
    // avoids accepting visually equivalent spellings under different byte forms.
    char[] utf16;
    if (!TextAlgorithms.Utf8ToUtf16(text, out utf16))
    {
        return false;
    }
    int status = 0;
    nint nfc = TextNative.unorm2_getNFCInstance(ref status);
    if (status > 0 ||
        !TextAlgorithms.IsNormalized(nfc, utf16, ref status) ||
        status > 0)
    {
        return false;
    }


    SequenceStatus a1_status = SequenceStatus.NotStarted;
    int a1_script = 1;
    SequenceStatus a2_status = SequenceStatus.NotStarted;
    int a2_script = 1;
    SequenceStatus b_status = SequenceStatus.NotStarted;
    int b_script = 1;

    for (int i = 0; i < codepoints.Count; ++i)
    {
        int codepoint = (int)(codepoints[i]);
        status = 0;
        int script = TextNative.uscript_getScript(codepoint, ref status);
        if (status > 0 ||
            TextNative.uscript_getUsage(script) != 5)
        {
            return false;
        }
        byte category = TextNative.u_charType(codepoint);
        int joining_type =
            TextNative.u_getIntPropertyValue(codepoint, (int)IcuUProperty.UCHAR_JOINING_TYPE);

        // Context rule A1: ZWNJ may follow a left/dual-joining sequence and must
        // eventually be followed, through transparent characters, by a
        // right/dual-joining character of the same effective script.
        if (a1_status == SequenceStatus.NearEnd)
        {
            if ((a1_script > 1) &&
                (script > 1) &&
                script != a1_script)
            {
                return false;
            }
            if (joining_type == (int)IcuUJoiningType.U_JT_RIGHT_JOINING ||
                joining_type == (int)IcuUJoiningType.U_JT_DUAL_JOINING)
            {
                a1_status = SequenceStatus.NotStarted;
            }
            else if (joining_type != (int)IcuUJoiningType.U_JT_TRANSPARENT)
            {
                return false;
            }
        }
        else if (a1_status == SequenceStatus.Started)
        {
            if ((a1_script > 1) &&
                (script > 1) &&
                script != a1_script)
            {
                a1_status = SequenceStatus.NotStarted;
            }
            else if (joining_type != (int)IcuUJoiningType.U_JT_TRANSPARENT)
            {
                if (codepoint == 0x200c)
                {
                    a1_status = SequenceStatus.NearEnd;
                    continue;
                }
                a1_status = SequenceStatus.NotStarted;
            }
        }
        if (a1_status == SequenceStatus.NotStarted &&
            (joining_type == (int)IcuUJoiningType.U_JT_LEFT_JOINING ||
             joining_type == (int)IcuUJoiningType.U_JT_DUAL_JOINING))
        {
            a1_status = SequenceStatus.Started;
            a1_script = script;
        }

        // Context rule A2: ZWNJ may follow a virama reached from a letter-like
        // sequence; following combining modifiers remain in the same script and
        // the sequence must return to a letter.
        if (a2_status == SequenceStatus.NearEnd)
        {
            if ((a2_script > 1) &&
                (script > 1) &&
                script != a2_script)
            {
                return false;
            }
            if (TextAlgorithms.IsLetterCategory(category))
            {
                a2_status = SequenceStatus.NotStarted;
            }
            else if (category != (int)IcuUCharCategory.U_MODIFIER_LETTER ||
                     TextNative.u_getCombiningClass(codepoint) == 0)
            {
                return false;
            }
        }
        else if (a2_status == SequenceStatus.StartedVirama)
        {
            if ((a2_script > 1) &&
                (script > 1) &&
                script != a2_script)
            {
                a2_status = SequenceStatus.NotStarted;
            }
            else if (codepoint == 0x200c)
            {
                a2_status = SequenceStatus.NearEnd;
                continue;
            }
            else if (category != (int)IcuUCharCategory.U_MODIFIER_LETTER ||
                     TextNative.u_getCombiningClass(codepoint) == 0)
            {
                a2_status = SequenceStatus.NotStarted;
            }
        }
        else if (a2_status == SequenceStatus.Started)
        {
            if ((a2_script > 1) &&
                (script > 1) &&
                script != a2_script)
            {
                a2_status = SequenceStatus.NotStarted;
            }
            else if (TextNative.u_getCombiningClass(codepoint) == 9)
            {
                a2_status = SequenceStatus.StartedVirama;
            }
            else if (category != (int)IcuUCharCategory.U_MODIFIER_LETTER)
            {
                a2_status = SequenceStatus.NotStarted;
            }
        }
        if (a2_status == SequenceStatus.NotStarted &&
            TextAlgorithms.IsLetterCategory(category))
        {
            a2_status = SequenceStatus.Started;
            a2_script = script;
        }

        // Context rule B: ZWJ may follow a virama reached from a letter-like
        // sequence. The following position is constrained by Indic syllabic
        // category so the joiner cannot create an arbitrary identifier sequence.
        if (b_status == SequenceStatus.NearEnd)
        {
            if ((b_script > 1) &&
                (script > 1) &&
                script != b_script)
            {
                return false;
            }
            if (TextNative.u_getIntPropertyValue(
                    codepoint,
                    (int)IcuUProperty.UCHAR_INDIC_SYLLABIC_CATEGORY
                ) != (int)IcuUIndicSyllabicCategory.U_INSC_VOWEL_DEPENDENT)
            {
                b_status = SequenceStatus.NotStarted;
            }
            else
            {
                return false;
            }
        }
        else if (b_status == SequenceStatus.StartedVirama)
        {
            if ((b_script > 1) &&
                (script > 1) &&
                script != b_script)
            {
                b_status = SequenceStatus.NotStarted;
            }
            else if (codepoint == 0x200d)
            {
                b_status = SequenceStatus.NearEnd;
                continue;
            }
            else if (category != (int)IcuUCharCategory.U_MODIFIER_LETTER ||
                     TextNative.u_getCombiningClass(codepoint) == 0)
            {
                b_status = SequenceStatus.NotStarted;
            }
        }
        else if (b_status == SequenceStatus.Started)
        {
            if ((b_script > 1) &&
                (script > 1) &&
                script != b_script)
            {
                b_status = SequenceStatus.NotStarted;
            }
            else if (TextNative.u_getCombiningClass(codepoint) == 9)
            {
                b_status = SequenceStatus.StartedVirama;
            }
            else if (category != (int)IcuUCharCategory.U_MODIFIER_LETTER)
            {
                b_status = SequenceStatus.NotStarted;
            }
        }
        if (b_status == SequenceStatus.NotStarted &&
            TextAlgorithms.IsLetterCategory(category))
        {
            b_status = SequenceStatus.Started;
            b_script = script;
        }

        // After contextual join controls are validated, apply the general
        // identifier profile: recommended scripts only, no pattern syntax,
        // whitespace or noncharacters, then position-specific start/continue
        // categories and the explicit compatibility exceptions below.
        if (TextNative.u_hasBinaryProperty(codepoint, (int)IcuUProperty.UCHAR_PATTERN_SYNTAX) ||
            TextNative.u_hasBinaryProperty(codepoint, (int)IcuUProperty.UCHAR_PATTERN_WHITE_SPACE) ||
            TextNative.u_hasBinaryProperty(codepoint, (int)IcuUProperty.UCHAR_NONCHARACTER_CODE_POINT))
        {
            return false;
        }

        bool identifier_start =
            TextAlgorithms.IsLetterCategory(category) ||
            category == (int)IcuUCharCategory.U_LETTER_NUMBER ||
            codepoint == 0x2118 ||
            codepoint == 0x212e ||
            codepoint == 0x309b ||
            codepoint == 0x309c ||
            codepoint == 0x005f;
        bool identifier_continue =
            identifier_start ||
            category == (int)IcuUCharCategory.U_NON_SPACING_MARK ||
            category == (int)IcuUCharCategory.U_COMBINING_SPACING_MARK ||
            category == (int)IcuUCharCategory.U_DECIMAL_DIGIT_NUMBER ||
            category == (int)IcuUCharCategory.U_CONNECTOR_PUNCTUATION ||
            codepoint == 0x1369 ||
            codepoint == 0x1371 ||
            codepoint == 0x00b7 ||
            codepoint == 0x0387 ||
            codepoint == 0x19da ||
            codepoint == 0x0e33 ||
            codepoint == 0x0eb3 ||
            codepoint == 0xff9e ||
            codepoint == 0xff9f;
        if (!(i == 0u ? identifier_start : identifier_continue))
        {
            return false;
        }
    }
    return true;
}

}
