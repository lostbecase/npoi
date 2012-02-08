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
using System.Collections.Generic;
using NPOI.OpenXmlFormats.Spreadsheet;
using NPOI.OpenXml4Net.OPC;
using NPOI.XSSF.UserModel;
using System.IO;
using System;
using NPOI.Util;
using System.Xml;

namespace NPOI.XSSF.Model
{


    /**
     * Table of styles shared across all sheets in a workbook.
     *
     * @author ugo
     */
    public class StylesTable : POIXMLDocumentPart
    {
        private Dictionary<int, String> numberFormats = new LinkedDictionary<int, String>();
        private List<XSSFFont> fonts = new List<XSSFFont>();
        private List<XSSFCellFill> Fills = new List<XSSFCellFill>();
        private List<XSSFCellBorder> borders = new List<XSSFCellBorder>();
        private List<CT_Xf> styleXfs = new List<CT_Xf>();
        private List<CT_Xf> xfs = new List<CT_Xf>();

        private List<CT_Dxf> dxfs = new List<CT_Dxf>();

        /**
         * The first style id available for use as a custom style
         */
        public static int FIRST_CUSTOM_STYLE_ID = BuiltinFormats.FIRST_USER_DEFINED_FORMAT_INDEX + 1;

        private StyleSheetDocument doc;
        private ThemesTable theme;

        /**
         * Create a new, empty StylesTable
         */
        public StylesTable()
            : base()
        {

            doc = StyleSheetDocument();
            doc.AddNewStyleSheet();
            // Initialization required in order to make the document Readable by MSExcel
            Initialize();
        }

        public StylesTable(PackagePart part, PackageRelationship rel)
            : base(part, rel)
        {

            ReadFrom(part.GetInputStream());
        }

        public ThemesTable GetTheme()
        {
            return theme;
        }

        public void SetTheme(ThemesTable theme)
        {
            this.theme = theme;

            // Pass the themes table along to things which need to 
            //  know about it, but have already been Created by now
            foreach (XSSFFont font in fonts)
            {
                font.SetThemesTable(theme);
            }
            foreach (XSSFCellBorder border in borders)
            {
                border.SetThemesTable(theme);
            }
        }

        /**
         * Read this shared styles table from an XML file.
         *
         * @param is The input stream Containing the XML document.
         * @throws IOException if an error occurs while Reading.
         */

        protected void ReadFrom(Stream is1)  {
		try {
			doc = StyleSheetDocument.Factory.Parse(is1);

            CT_Stylesheet styleSheet = doc.GetStyleSheet();

            // Grab all the different bits we care about
			CT_NumFmts ctfmts = styleSheet.numFmts;
            if( ctfmts != null){
                foreach (CT_NumFmt nfmt in ctfmts.GetNumFmtArray()) {
                    numberFormats.Put((int)nfmt.GetNumFmtId(), nfmt.GetFormatCode());
                }
            }

            CT_Fonts ctfonts = styleSheet.fonts;
            if(ctfonts != null){
				int idx = 0;
				foreach (CT_Font font in ctfonts.GetFontArray()) {
				   // Create the font and save it. Themes Table supplied later
					XSSFFont f = new XSSFFont(font, idx);
					fonts.Add(f);
					idx++;
				}
			}
            CT_Fills ctFills = styleSheet.fills;
            if(ctFills != null){
                foreach (CT_Fill fill in ctFills.GetFillArray()) {
                    Fills.Add(new XSSFCellFill(Fill));
                }
            }

            CT_Borders ctborders = styleSheet.borders;
            if(ctborders != null) {
                foreach (CT_Border border in ctborders.GetBorderArray()) {
                    borders.Add(new XSSFCellBorder(border));
                }
            }

            CT_CellXfs cellXfs = styleSheet.cellXfs;
            if(cellXfs != null) xfs.AddRange(Arrays.AsList(cellXfs.GetXfArray()));

            CT_CellStyleXfs cellStyleXfs = styleSheet.cellStyleXfs;
            if(cellStyleXfs != null) styleXfs.AddRange(Arrays.AsList(cellStyleXfs.GetXfArray()));

            CT_Dxfs styleDxfs = styleSheet.GetDxfs();
            if (styleDxfs != null) dxfs.AddRange(Arrays.AsList(styleDxfs.GetDxfArray()));

		} catch (XmlException e) {
			throw new IOException(e.Message);
		}
	}

        // ===========================================================
        //  Start of style related Getters and Setters
        // ===========================================================

        public String GetNumberFormatAt(int idx)
        {
            return numberFormats[idx];
        }

        public int PutNumberFormat(String fmt) {
		if (numberFormats.ContainsValue(fmt)) {
			// Find the key, and return that
			foreach(int key in numberFormats.Keys ) {
				if(numberFormats[key].Equals(fmt)) {
					return key;
				}
			}
			throw new InvalidOperationException("Found the format, but couldn't figure out where - should never happen!");
		}

		// Find a spare key, and add that
		int newKey = FIRST_CUSTOM_STYLE_ID;
		while(numberFormats.ContainsKey(newKey)) {
			newKey++;
		}
		numberFormats[newKey]= fmt;
		return newKey;
	}

        public XSSFFont GetFontAt(int idx)
        {
            return fonts[idx];
        }

        /**
         * Records the given font in the font table.
         * Will re-use an existing font index if this
         *  font matches another, EXCEPT if forced
         *  registration is requested.
         * This allows people to create several fonts
         *  then customise them later.
         * Note - End Users probably want to call
         *  {@link XSSFFont#registerTo(StylesTable)}
         */
        public int PutFont(XSSFFont font, bool forceRegistration)
        {
            int idx = -1;
            if (!forceRegistration)
            {
                idx = fonts.IndexOf(font);
            }

            if (idx != -1)
            {
                return idx;
            }

            idx = fonts.Count;
            fonts.Add(font);
            return idx;
        }
        public int PutFont(XSSFFont font)
        {
            return PutFont(font, false);
        }

        public XSSFCellStyle GetStyleAt(int idx)
        {
            int styleXfId = 0;

            // 0 is the empty default
            if (xfs[idx].xfId > 0)
            {
                styleXfId = (int)xfs[idx].xfId;
            }

            return new XSSFCellStyle(idx, styleXfId, this, theme);
        }
        public int PutStyle(XSSFCellStyle style)
        {
            CT_Xf mainXF = style.GetCoreXf();

            if (!xfs.Contains(mainXF))
            {
                xfs.Add(mainXF);
            }
            return xfs.IndexOf(mainXF);
        }

        public XSSFCellBorder GetBorderAt(int idx)
        {
            return borders[idx];
        }

        public int PutBorder(XSSFCellBorder border)
        {
            int idx = borders.IndexOf(border);
            if (idx != -1)
            {
                return idx;
            }
            borders.Add(border);
            border.SetThemesTable(theme);
            return borders.Count - 1;
        }

        public XSSFCellFill GetFillAt(int idx)
        {
            return Fills[idx];
        }

        public List<XSSFCellBorder> GetBorders()
        {
            return borders;
        }

        public List<XSSFCellFill> GetFills()
        {
            return Fills;
        }

        public List<XSSFFont> GetFonts()
        {
            return fonts;
        }

        public Dictionary<int, String> GetNumberFormats()
        {
            return numberFormats;
        }

        public int PutFill(XSSFCellFill Fill)
        {
            int idx = Fills.IndexOf(Fill);
            if (idx != -1)
            {
                return idx;
            }
            Fills.Add(Fill);
            return Fills.Count - 1;
        }

        public CT_Xf GetCellXfAt(int idx)
        {
            return xfs[idx];
        }
        public int PutCellXf(CT_Xf cellXf)
        {
            xfs.Add(cellXf);
            return xfs.Count;
        }
        public void ReplaceCellXfAt(int idx, CT_Xf cellXf)
        {
            xfs[idx] = cellXf;
        }

        public CT_Xf GetCellStyleXfAt(int idx)
        {
            return styleXfs[idx];
        }
        public int PutCellStyleXf(CT_Xf cellStyleXf)
        {
            styleXfs.Add(cellStyleXf);
            return styleXfs.Count;
        }
        public void ReplaceCellStyleXfAt(int idx, CT_Xf cellStyleXf)
        {
            styleXfs[idx] = cellStyleXf;
        }

        /**
         * get the size of cell styles
         */
        public int GetNumCellStyles()
        {
            // Each cell style has a unique xfs entry
            // Several might share the same styleXfs entry
            return xfs.Count;
        }

        /**
         * For unit testing only
         */
        public int _GetNumberFormatSize()
        {
            return numberFormats.Count;
        }

        /**
         * For unit testing only
         */
        public int _GetXfsSize()
        {
            return xfs.Count;
        }
        /**
         * For unit testing only
         */
        public int _GetStyleXfsSize()
        {
            return styleXfs.Count;
        }
        /**
         * For unit testing only!
         */
        public CT_Stylesheet GetCT_Stylesheet()
        {
            return doc.GetStyleSheet();
        }
        public int _GetDXfsSize()
        {
            return dxfs.Count;
        }


        /**
         * Write this table out as XML.
         *
         * @param out The stream to write to.
         * @throws IOException if an error occurs while writing.
         */
        public void WriteTo(Stream out1)  {

		// Work on the current one
		// Need to do this, as we don't handle
		//  all the possible entries yet
        CT_Stylesheet styleSheet = doc.GetStyleSheet();

		// Formats
		CT_NumFmts formats = new CT_NumFmts();
		formats.count = (uint)numberFormats.Count;
		foreach (KeyValuePair<int, String> fmt in numberFormats) {
			CT_NumFmt ctFmt = formats.AddNewNumFmt();
			ctFmt.numFmtId = (uint)fmt.Key;
			ctFmt.formatCode = fmt.Value;
		}
		styleSheet.numFmts = (formats);

		int idx;
		// Fonts
		CT_Fonts ctFonts = new CT_Fonts();
		ctFonts.count = (uint)fonts.Count;
		CT_Font[] ctfnt = new CT_Font[fonts.Count];
		idx = 0;
		foreach(XSSFFont f in fonts) 
            ctfnt[idx++] = f.GetCTFont();
		ctFonts.SetFontArray(ctfnt);
		styleSheet.fonts = (ctFonts);

		// Fills
		CT_Fills ctFills = new CT_Fills();
		ctFills.count = (uint)Fills.Count;
		CT_Fill[] ctf = new CT_Fill[Fills.Count];
		idx = 0;
		foreach(XSSFCellFill f in Fills) 
            ctf[idx++] = f.GetCTFill();
		ctFills.SetFillArray(ctf);
		styleSheet.fills = (ctFills);

		// Borders
		CT_Borders ctBorders = new CT_Borders();
		ctBorders.count = (uint)borders.Count;
		CT_Border[] ctb = new CT_Border[borders.Count];
		idx = 0;
		foreach(XSSFCellBorder b in borders) ctb[idx++] = b.GetCTBorder();
		ctBorders.SetBorderArray(ctb);
		styleSheet.borders = (ctBorders);

		// Xfs
		if(xfs.Count > 0) {
			CT_CellXfs ctXfs = new CT_CellXfs();
			ctXfs.count = (uint)xfs.Count;
			ctXfs.xf = xfs;
			
			styleSheet.cellXfs = (ctXfs);
		}

		// Style xfs
		if(styleXfs.Count > 0) {
			CT_CellStyleXfs ctSXfs = new CT_CellStyleXfs();
			ctSXfs.count = (uint)(styleXfs.Count);
			ctSXfs.xf= styleXfs;
			
			styleSheet.cellStyleXfs = (ctSXfs);
		}

		// Style dxfs
		if(dxfs.Count > 0) {
			CT_Dxfs ctDxfs = new CT_Dxfs();
			ctDxfs.count = (uint)dxfs.Count;
			ctDxfs.dxf = dxfs;
			
			styleSheet.dxfs = (ctDxfs);
		}

		// Save
		doc.save(out1);
	}


        protected void Commit()
        {
            PackagePart part = GetPackagePart();
            Stream out1 = part.GetOutputStream();
            WriteTo(out1);
            out1.Close();
        }

        private void Initialize()
        {
            //CT_Font ctFont = CreateDefaultFont();
            XSSFFont xssfFont = CreateDefaultFont();
            fonts.Add(xssfFont);

            CT_Fill[] ctFill = CreateDefaultFills();
            Fills.Add(new XSSFCellFill(ctFill[0]));
            Fills.Add(new XSSFCellFill(ctFill[1]));

            CT_Border ctBorder = CreateDefaultBorder();
            borders.Add(new XSSFCellBorder(ctBorder));

            CT_Xf styleXf = CreateDefaultXf();
            styleXfs.Add(styleXf);
            CT_Xf xf = CreateDefaultXf();
            xf.xfId = (0);
            xfs.Add(xf);
        }

        private static CT_Xf CreateDefaultXf()
        {
            CT_Xf ctXf = new CT_Xf();
            ctXf.numFmtId = (0);
            ctXf.fontId = (0);
            ctXf.fillId = (0);
            ctXf.borderId = (0);
            return ctXf;
        }
        private static CT_Border CreateDefaultBorder()
        {
            CT_Border ctBorder = new CT_Border();
            ctBorder.AddNewBottom();
            ctBorder.AddNewTop();
            ctBorder.AddNewLeft();
            ctBorder.AddNewRight();
            ctBorder.AddNewDiagonal();
            return ctBorder;
        }


        private static CT_Fill[] CreateDefaultFills()
        {
            CT_Fill[] ctFill = new CT_Fill[] { new CT_Fill(), new CT_Fill() };
            ctFill[0].AddNewPatternFill().patternType = (ST_PatternType.none);
            ctFill[1].AddNewPatternFill().patternType = (ST_PatternType.darkGray);
            return ctFill;
        }

        private static XSSFFont CreateDefaultFont()
        {
            CT_Font ctFont = new CT_Font();
            XSSFFont xssfFont = new XSSFFont(ctFont, 0);
            xssfFont.FontHeightInPoints =(XSSFFont.DEFAULT_FONT_SIZE);
            xssfFont.Color = (XSSFFont.DEFAULT_FONT_COLOR);//SetTheme
            xssfFont.FontName = (XSSFFont.DEFAULT_FONT_NAME);
            xssfFont.SetFamily(FontFamily.SWISS);
            xssfFont.SetScheme(FontScheme.MINOR);
            return xssfFont;
        }

        public CT_Dxf GetDxfAt(int idx)
        {
            return dxfs[idx];
        }

        public int PutDxf(CT_Dxf dxf)
        {
            this.dxfs.Add(dxf);
            return this.dxfs.Count;
        }

        public XSSFCellStyle CreateCellStyle()
        {
            CT_Xf xf = new CT_Xf();
            xf.numFmtId = (0);
            xf.fontId = (0);
            xf.fillId = (0);
            xf.borderId = (0);
            xf.xfId = (0);
            int xfSize = styleXfs.Count;
            int indexXf = PutCellXf(xf);
            return new XSSFCellStyle(indexXf - 1, xfSize - 1, this, theme);
        }

        /**
         * Finds a font that matches the one with the supplied attributes
         */
        public XSSFFont FindFont(short boldWeight, short color, short fontHeight, String name, bool italic, bool strikeout, short typeOffset, byte underline)
        {
            foreach (XSSFFont font in fonts)
            {
                if ((font.Boldweight == boldWeight)
                        && font.Color == color
                        && font.FontHeight == fontHeight
                        && font.FontName.Equals(name)
                        && font.IsItalic == italic
                        && font.IsStrikeout == strikeout
                        && font.TypeOffset == typeOffset
                        && font.Underline == underline)
                {
                    return font;
                }
            }
            return null;
        }
    }
}





