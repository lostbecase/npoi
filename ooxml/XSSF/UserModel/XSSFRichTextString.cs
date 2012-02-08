/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

using NPOI.SS.UserModel;
using System.Text.RegularExpressions;
using NPOI.OpenXmlFormats.Spreadsheet;
using System;
using System.Text;
using System.Collections.Generic;
namespace NPOI.XSSF.UserModel
{

    /**
     * Rich text unicode string.  These strings can have fonts applied to arbitary parts of the string.
     *
     * <p>
     * Most strings in a workbook have formatting applied at the cell level, that is, the entire string in the cell has the
     * same formatting applied. In these cases, the formatting for the cell is stored in the styles part,
     * and the string for the cell can be shared across the workbook. The following code illustrates the example.
     * </p>
     *
     * <blockquote>
     * <pre>
     *     cell1.SetCellValue(new XSSFRichTextString("Apache POI"));
     *     cell2.SetCellValue(new XSSFRichTextString("Apache POI"));
     *     cell3.SetCellValue(new XSSFRichTextString("Apache POI"));
     * </pre>
     * </blockquote>
     * In the above example all three cells will use the same string cached on workbook level.
     *
     * <p>
     * Some strings in the workbook may have formatting applied at a level that is more granular than the cell level.
     * For instance, specific characters within the string may be bolded, have coloring, italicizing, etc.
     * In these cases, the formatting is stored along with the text in the string table, and is treated as
     * a unique entry in the workbook. The following xml and code snippet illustrate this.
     * </p>
     *
     * <blockquote>
     * <pre>
     *     XSSFRichTextString s1 = new XSSFRichTextString("Apache POI");
     *     s1.ApplyFont(boldArial);
     *     cell1.SetCellValue(s1);
     *
     *     XSSFRichTextString s2 = new XSSFRichTextString("Apache POI");
     *     s2.ApplyFont(italicCourier);
     *     cell2.SetCellValue(s2);
     * </pre>
     * </blockquote>
     *
     *
     * @author Yegor Kozlov
     */
    public class XSSFRichTextString : IRichTextString
    {
        private static Regex utfPtrn = new Regex("_x([0-9A-F]{4})_");

        private CT_Rst st;
        private StylesTable styles;

        /**
         * Create a rich text string
         */
        public XSSFRichTextString(String str)
        {
            st = new CT_Rst();
            st.t = str;
            preserveSpaces(st.xgetT());
        }

        /**
         * Create empty rich text string and Initialize it with empty string
         */
        public XSSFRichTextString()
        {
            st = new CT_Rst();
        }

        /**
         * Create a rich text string from the supplied XML bean
         */
        public XSSFRichTextString(CT_Rst st)
        {
            this.st = st;
        }

        /**
         * Applies a font to the specified characters of a string.
         *
         * @param startIndex    The start index to apply the font to (inclusive)
         * @param endIndex      The end index to apply the font to (exclusive)
         * @param fontIndex     The font to use.
         */
        public void ApplyFont(int startIndex, int endIndex, short fontIndex)
        {
            XSSFFont font;
            if (styles == null)
            {
                //style table is not Set, remember fontIndex and Set the run properties later,
                //when SetStylesTableReference is called
                font = new XSSFFont();
                font.SetFontName("#" + fontIndex);
            }
            else
            {
                font = styles.GetFontAt(fontIndex);
            }
            ApplyFont(startIndex, endIndex, font);
        }

        /**
         * Applies a font to the specified characters of a string.
         *
         * @param startIndex    The start index to apply the font to (inclusive)
         * @param endIndex      The end index to apply to font to (exclusive)
         * @param font          The index of the font to use.
         */
        public void ApplyFont(int startIndex, int endIndex, IFont font)
        {
            if (startIndex > endIndex)
                throw new ArgumentException("Start index must be less than end index.");
            if (startIndex < 0 || endIndex > length())
                throw new ArgumentException("Start and end index not in range.");
            if (startIndex == endIndex)
                return;

            if (st.sizeOfRArray() == 0 && st.IsSetT())
            {
                //convert <t>string</t> into a text Run: <r><t>string</t></r>
                st.AddNewR().SetT(st.GetT());
                st.unsetT();
            }

            String text = GetString();
            XSSFFont xssfFont = (XSSFFont)font;

            TreeDictionary<int, CTRPrElt> formats = GetFormatMap(st);
            CTRPrElt fmt = CTRPrElt.Factory.newInstance();
            SetRunAttributes(xssfFont.GetCTFont(), fmt);
            ApplyFont(formats, startIndex, endIndex, fmt);

            CT_Rst newSt = buildCT_Rst(text, formats);
            st.Set(newSt);
        }

        /**
         * Sets the font of the entire string.
         * @param font          The font to use.
         */
        public void ApplyFont(Font font)
        {
            String text = GetString();
            ApplyFont(0, text.Length, font);
        }

        /**
         * Applies the specified font to the entire string.
         *
         * @param fontIndex  the font to Apply.
         */
        public void ApplyFont(short fontIndex)
        {
            XSSFFont font;
            if (styles == null)
            {
                font = new XSSFFont();
                font.SetFontName("#" + fontIndex);
            }
            else
            {
                font = styles.GetFontAt(fontIndex);
            }
            String text = GetString();
            ApplyFont(0, text.Length, font);
        }

        /**
         * Append new text to this text run and apply the specify font to it
         *
         * @param text  the text to append
         * @param font  the font to apply to the Appended text or <code>null</code> if no formatting is required
         */
        public void Append(String text, XSSFFont font)
        {
            if (st.sizeOfRArray() == 0 && st.IsSetT())
            {
                //convert <t>string</t> into a text Run: <r><t>string</t></r>
                st.AddNewR().SetT(st.GetT());
                st.unsetT();
            }
            CTRElt lt = st.AddNewR();
            lt.SetT(text);
            CTRPrElt pr = lt.AddNewRPr();
            if (font != null) SetRunAttributes(font.GetCTFont(), pr);
        }

        /**
         * Append new text to this text run
         *
         * @param text  the text to append
         */
        public void Append(String text)
        {
            Append(text, null);
        }

        /**
         * Copy font attributes from CTFont bean into CTRPrElt bean
         */
        private void SetRunAttributes(CT_Font ctFont, CT_RPrElt pr)
        {
            if (ctFont.sizeOfBArray() > 0) pr.AddNewB().SetVal(ctFont.GetBArray(0).GetVal());
            if (ctFont.sizeOfUArray() > 0) pr.AddNewU().SetVal(ctFont.GetUArray(0).GetVal());
            if (ctFont.sizeOfIArray() > 0) pr.AddNewI().SetVal(ctFont.GetIArray(0).GetVal());
            if (ctFont.sizeOfColorArray() > 0)
            {
                CT_Color c1 = ctFont.GetColorArray(0);
                CT_Color c2 = pr.AddNewColor();
                if (c1.IsSetAuto()) c2.SetAuto(c1.GetAuto());
                if (c1.IsSetIndexed()) c2.SetIndexed(c1.GetIndexed());
                if (c1.IsSetRgb()) c2.SetRgb(c1.GetRgb());
                if (c1.IsSetTheme()) c2.SetTheme(c1.GetTheme());
                if (c1.IsSetTint()) c2.SetTint(c1.GetTint());
            }
            if (ctFont.sizeOfSzArray() > 0) pr.AddNewSz().SetVal(ctFont.GetSzArray(0).GetVal());
            if (ctFont.sizeOfNameArray() > 0) pr.AddNewRFont().SetVal(ctFont.GetNameArray(0).GetVal());
            if (ctFont.sizeOfFamilyArray() > 0) pr.AddNewFamily().SetVal(ctFont.GetFamilyArray(0).GetVal());
            if (ctFont.sizeOfSchemeArray() > 0) pr.AddNewScheme().SetVal(ctFont.GetSchemeArray(0).GetVal());
            if (ctFont.sizeOfCharsetArray() > 0) pr.AddNewCharset().SetVal(ctFont.GetCharsetArray(0).GetVal());
            if (ctFont.sizeOfCondenseArray() > 0) pr.AddNewCondense().SetVal(ctFont.GetCondenseArray(0).GetVal());
            if (ctFont.sizeOfExtendArray() > 0) pr.AddNewExtend().SetVal(ctFont.GetExtendArray(0).GetVal());
            if (ctFont.sizeOfVertAlignArray() > 0) pr.AddNewVertAlign().SetVal(ctFont.GetVertAlignArray(0).GetVal());
            if (ctFont.sizeOfOutlineArray() > 0) pr.AddNewOutline().SetVal(ctFont.GetOutlineArray(0).GetVal());
            if (ctFont.sizeOfShadowArray() > 0) pr.AddNewShadow().SetVal(ctFont.GetShadowArray(0).GetVal());
            if (ctFont.sizeOfStrikeArray() > 0) pr.AddNewStrike().SetVal(ctFont.GetStrikeArray(0).GetVal());
        }

        /**
         * Removes any formatting that may have been applied to the string.
         */
        public void ClearFormatting()
        {
            String text = GetString();
            st.SetRArray(null);
            st.SetT(text);
        }

        /**
         * The index within the string to which the specified formatting run applies.
         *
         * @param index     the index of the formatting run
         * @return  the index within the string.
         */
        public int GetIndexOfFormattingRun(int index)
        {
            if (st.sizeOfRArray() == 0) return 0;

            int pos = 0;
            for (int i = 0; i < st.sizeOfRArray(); i++)
            {
                CTRElt r = st.GetRArray(i);
                if (i == index) return pos;

                pos += r.GetT().Length;
            }
            return -1;
        }

        /**
         * Returns the number of characters this format run covers.
         *
         * @param index     the index of the formatting run
         * @return  the number of characters this format run covers
         */
        public int GetLengthOfFormattingRun(int index)
        {
            if (st.sizeOfRArray() == 0) return length();

            for (int i = 0; i < st.sizeOfRArray(); i++)
            {
                CTRElt r = st.GetRArray(i);
                if (i == index) return r.GetT().Length;
            }
            return -1;
        }


        /**
         * Removes any formatting and Sets new string value
         *
         * @param s new string value
         */
        public void SetString(String s)
        {
            ClearFormatting();
            st.t = (s);
            preserveSpaces(st.xgetT());
        }
        public String GetString()
        {
            if (st.sizeOfRArray() == 0)
            {
                return UtfDecode(st.t);
            }
            StringBuilder buf = new StringBuilder();
            foreach (CT_RElt r in st.r)
            {
                buf.Append(r.t);
            }
            return UtfDecode(buf.ToString());
        }

        /**
         * Returns the plain string representation.
         */
        public override String ToString()
        {
            return GetString();
        }

        /**
         * Returns the number of characters in this string.
         */
        public int Length()
        {
            return GetString().Length;
        }

        /**
         * @return  The number of formatting Runs used.
         */
        public int numFormattingRuns()
        {
            return st.sizeOfRArray();
        }

        /**
         * Gets a copy of the font used in a particular formatting Run.
         *
         * @param index     the index of the formatting run
         * @return  A copy of the  font used or null if no formatting is applied to the specified text Run.
         */
        public XSSFFont GetFontOfFormattingRun(int index)
        {
            if (st.sizeOfRArray() == 0) return null;

            for (int i = 0; i < st.sizeOfRArray(); i++)
            {
                CT_RElt r = st.GetRArray(i);
                if (i == index)
                {
                    XSSFFont fnt = new XSSFFont(ToCTFont(r.rPr));
                    fnt.SetThemesTable(GetThemesTable());
                    return fnt;
                }
            }
            return null;
        }

        /**
         * Return a copy of the font in use at a particular index.
         *
         * @param index         The index.
         * @return              A copy of the  font that's currently being applied at that
         *                      index or null if no font is being applied or the
         *                      index is out of range.
         */
        public XSSFFont GetFontAtIndex(int index)
        {
            if (st.sizeOfRArray() == 0) return null;

            int pos = 0;
            for (int i = 0; i < st.sizeOfRArray(); i++)
            {
                CT_RElt r = st.GetRArray(i);
                if (index >= pos && index < pos + r.t.Length)
                {
                    XSSFFont fnt = new XSSFFont(ToCTFont(r.rPr));
                    fnt.SetThemesTable(GetThemesTable());
                    return fnt;
                }

                pos += r.GetT().Length;
            }
            return null;

        }

        /**
         * Return the underlying xml bean
         */

        public CT_Rst GetCTRst()
        {
            return st;
        }


        /**
         *
         * CTRPrElt --> CTFont adapter
         */
        protected static CT_Font ToCTFont(CT_RPrElt pr)
        {
            CT_Font ctFont = new CT_Font();

            if (pr.sizeOfBArray() > 0) ctFont.AddNewB().SetVal(pr.GetBArray(0).GetVal());
            if (pr.sizeOfUArray() > 0) ctFont.AddNewU().SetVal(pr.GetUArray(0).GetVal());
            if (pr.sizeOfIArray() > 0) ctFont.AddNewI().SetVal(pr.GetIArray(0).GetVal());
            if (pr.sizeOfColorArray() > 0)
            {
                CT_Color c1 = pr.GetColorArray(0);
                CT_Color c2 = ctFont.AddNewColor();
                if (c1.IsSetAuto()) c2.auto = (c1.auto);
                if (c1.IsSetIndexed()) c2.indexed = (c1.indexed);
                if (c1.IsSetRgb()) c2.SetRgb(c1.GetRgb());
                if (c1.IsSetTheme()) c2.theme = (c1.theme);
                if (c1.IsSetTint()) c2.tint = (c1.tint);
            }
            if (pr.sizeOfSzArray() > 0) ctFont.AddNewSz().SetVal(pr.GetSzArray(0).GetVal());
            if (pr.sizeOfRFontArray() > 0) ctFont.AddNewName().SetVal(pr.GetRFontArray(0).GetVal());
            if (pr.sizeOfFamilyArray() > 0) ctFont.AddNewFamily().SetVal(pr.GetFamilyArray(0).GetVal());
            if (pr.sizeOfSchemeArray() > 0) ctFont.AddNewScheme().SetVal(pr.GetSchemeArray(0).GetVal());
            if (pr.sizeOfCharsetArray() > 0) ctFont.AddNewCharset().SetVal(pr.GetCharsetArray(0).GetVal());
            if (pr.sizeOfCondenseArray() > 0) ctFont.AddNewCondense().SetVal(pr.GetCondenseArray(0).GetVal());
            if (pr.sizeOfExtendArray() > 0) ctFont.AddNewExtend().SetVal(pr.GetExtendArray(0).GetVal());
            if (pr.sizeOfVertAlignArray() > 0) ctFont.AddNewVertAlign().SetVal(pr.GetVertAlignArray(0).GetVal());
            if (pr.sizeOfOutlineArray() > 0) ctFont.AddNewOutline().SetVal(pr.GetOutlineArray(0).GetVal());
            if (pr.sizeOfShadowArray() > 0) ctFont.AddNewShadow().SetVal(pr.GetShadowArray(0).GetVal());
            if (pr.sizeOfStrikeArray() > 0) ctFont.AddNewStrike().SetVal(pr.GetStrikeArray(0).GetVal());

            return ctFont;
        }

        /**
         * Add the xml:spaces="preserve" attribute if the string has leading or trailing spaces
         *
         * @param xs    the string to check
         */
        protected static void preserveSpaces(ST_Xstring xs)
        {
            String text = xs.StringValue;
            if (text != null && text.Length > 0)
            {
                char firstChar = text[0];
                char lastChar = text[text.Length - 1];
                if (Char.IsWhiteSpace(firstChar) || Char.IsWhiteSpace(lastChar))
                {
                    XmlCursor c = xs.newCursor();
                    c.ToNextToken();
                    c.insertAttributeWithValue(new QName("http://www.w3.org/XML/1998/namespace", "space"), "preserve");
                    c.dispose();
                }
            }
        }

        /**
         * For all characters which cannot be represented in XML as defined by the XML 1.0 specification,
         * the characters are escaped using the Unicode numerical character representation escape character
         * format _xHHHH_, where H represents a hexadecimal character in the character's value.
         * <p>
         * Example: The Unicode character 0D is invalid in an XML 1.0 document,
         * so it shall be escaped as <code>_x000D_</code>.
         * </p>
         * See section 3.18.9 in the OOXML spec.
         *
         * @param   value the string to decode
         * @return  the decoded string
         */
        static String UtfDecode(String value)
        {
            if (value == null) return null;

            StringBuilder buf = new StringBuilder();
            MatchCollection mc = utfPtrn.Matches(value);
            int idx = 0;
            for (int i = 0; i < mc.Count;i++ )
            {
                    int pos = mc[i].Index;
                    if (pos > idx)
                    {
                        buf.Append(value.Substring(idx, pos));
                    }

                    String code = mc[i].Groups[1].Value;
                    int icode = Int32.Parse("0x" + code);
                    buf.Append((char)icode);

                    idx = mc[i].Index+mc[i].Length;
                }
            buf.Append(value.Substring(idx));
            return buf.ToString();
        }



        CT_Rst buildCT_Rst(String text, Dictionary<int, CT_RPrElt> formats)
        {
            if (text.Length != formats.lastKey())
            {
                throw new ArgumentException("Text length was " + text.Length +
                        " but the last format index was " + formats.lastKey());
            }
            CT_Rst st = new CT_Rst();
            int RunStartIdx = 0;
            for (Iterator<int> it = formats.keySet().iterator(); it.HasNext(); )
            {
                int RunEndIdx = it.next();
                CT_RElt run = st.AddNewR();
                String fragment = text.Substring(RunStartIdx, RunEndIdx);
                run.t = (fragment);
                preserveSpaces(run.xgetT());
                CT_RPrElt fmt = formats.Get(RunEndIdx);
                if (fmt != null)
                    run.rPr = (fmt);
                RunStartIdx = RunEndIdx;
            }
            return st;
        }

        private ThemesTable GetThemesTable()
        {
            if (styles == null) return null;
            return styles.GetTheme();
        }
    }
}

