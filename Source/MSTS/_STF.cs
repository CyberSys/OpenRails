/// This file contains code to read MSTS structured unicode text files
/// through the class  STFReader.   
/// 
/// Note:  the SBR classes are more general in that they are capable of reading
///        both unicode and binary compressed data files.

/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace MSTS
{
    /// <summary>Used for reading data from Structured Text Format (MSTS1 style) files.
    /// </summary><remarks><para>
    /// An STF file is whitespace delimitered file, taking the format - {item}{whitespace}[repeated].</para><para>
    /// &#160;</para><para>
    /// At it's most simple an STF file has the format - {token_item}{whitespace}{data_item}{whitespace}(repeated)</para><para>
    /// Even, more simplisitically every {data_item} can be a {constant_item}</para>
    /// <code lang="STF" title="STF Example"><para>
    ///     Example:</para><para>
    ///     name SimpleSTFfile</para><para>
    ///     weight 100</para><para>
    ///     speed 50.25</para>
    /// </code>&#160;<para>
    /// STF also has a block methodology where a {data_item} following a {token_item} can start with '(' followed by any number of {data_item}s and closed with a ')'.
    /// The contents of the block are defined in the specific file schema, and not in the STF definition.
    /// The STF defintion allows that inside a pair of parentheses may be a single {constant_item}, multiple whitespace delimitered {constant_item}s, or a nested {token_item}{data_item} pair (which could contain a further nested block recursively).</para>
    /// <code lang="STF" title="STF Example"><para>
    ///     Example:</para><para>
    ///     name BlockedSTFfile</para><para>
    ///     root_constant 100</para><para>
    ///     root_block_1</para><para>
    ///     (</para><para>
    ///         &#160;&#160;nested_block_1_1</para><para>
    ///         &#160;&#160;(</para><para>
    ///             &#160;&#160;&#160;&#160;1</para><para>
    ///         &#160;&#160;)</para><para>
    ///         &#160;&#160;nested_block_1_2 ( 5 )</para><para>
    ///     )</para><para>
    ///     root_block_2</para><para>
    ///     (</para><para>
    ///         &#160;&#160;1 2 3</para><para>
    ///     )</para><para>
    ///     root_block_3 ( a b c )</para>
    /// </code>&#160;<para>
    /// Numeric {constan_item}s can include a 'unit' suffix, which is handled in the ReadDouble() function.</para><para>
    /// Within ReadDouble these units are then converted to the standards used throughout OR - meters, newtons, kilograms.</para>
    /// <code lang="STF" title="STF Example"><para>
    ///     Example:</para><para>
    ///     name STFfileWithUnits</para><para>
    ///     weight 100kg</para><para>
    ///     speed 50mph</para>
    /// </code>&#160;<para>
    /// Whitespaces can be included within any {item} using a double quotation notation.
    /// Quoted values also support a trailing addition operator to indicate an append operation of multiple quoted strings.</para><para>
    /// Although append operations are technically allowed for {token_item}'s this practice is *strongly* discouraged for readability.</para>
    /// <code lang="STF" title="STF Example"><para>
    ///     Example:</para><para>
    ///     simple_token "Data Item with" + " whitespace"</para><para>
    ///     block_token ( "Data " + "Item 1" "Data Item 2" )</para><para>
    ///     "discouraged_" + "token" -1</para><para>
    ///     Error Example:</para><para>
    ///     error1 "You cannot use append suffix to non quoted " + items</para>
    /// </code>&#160;<para>
    /// The STF format also supports 3 special {token_item}s - include, comment &amp; skip.</para><list class="bullet">
    /// <listItem><para>include - must be at the root level (that is to say it cannot be included within a block).
    /// After an include directive the {constant_item} is a filename relative to the current processing STF file.
    /// The include token has the effect of in-lining the defined file into the current document.</para></listItem>
    /// <listItem><para>comment &amp; skip - must be followed by a block which will not be processed in OR</para></listItem>
    /// </list>&#160;<para>
    /// Finally any token which begins with a '#' character will be ignored, and then the next {data_item} (constant or block) will not be processed.</para><para>
    /// &#160;</para>
    /// <alert class="important"><para>NB!!! If a comment/skip/#*/_* is the last {item} in a block, rather than being totally consumed a dummy '#' is returned, so if EndOFBlock() returns false, you always get an {item} (which can then just be ignored).</para></alert>
    /// </remarks>
    /// <example><code lang="C#" title="Typical STF parsing using C#">
    ///        using (STFReader f = new STFReader(filename))
    ///        {
    ///            while (!f.EOF)
    ///                switch (f.ReadItem().ToLower())
    ///                {
    ///                    case "item_single_constant": float isc = f.ReadFloat(STFReader.UNITS.None, 0); break;
    ///                    case "item_single_speed": float iss_mps = f.ReadFloat(STFReader.UNITS.Speed, 0); break;
    ///                    case "block_single_constant": float bsc = f.ReadFloatBlock(STFReader.UNITS.None, 0); break;
    ///                    case "block_fixed_format":
    ///                        f.MustMatch("(");
    ///                        int bff1 = f.ReadInt(STFReader.UNITS.None, 0);
    ///                        string bff2 = f.ReadItem();
    ///                        f.SkipRestOfBlock();
    ///                        break;
    ///                    case "block_variable_contents":
    ///                        f.MustMatch("(");
    ///                        while (!f.EndOfBlock())
    ///                            switch (f.ReadItem().ToLower())
    ///                            {
    ///                                case "subitem": string si = f.ReadItem(); break;
    ///                                case "subblock": string sb = f.ReadItemBlock(""); break;
    ///                                case "(": f.SkipRestOfBlock();
    ///                            }
    ///                        break;
    ///                    case "(": f.SkipRestOfBlock(); break;
    ///                }
    ///        }
    /// </code></example>
    /// <exception cref="STFException"><para>
    /// STF reports errors using the  exception static members</para><para>
    /// There are three broad categories of error</para><list class="bullet">
    /// <listItem><para>Failure - Something which prevents loading from continuing, this throws an unhandled exception and drops out of Open Rails.</para></listItem>
    /// <listItem><para>Error - The data read does not have logical meaning - STFReader does not generate these errors, this is only appropriate STFReader consumers who understand the context of the data being processed</para></listItem>
    /// <listItem><para>Warning - When an error which can be programatically recovered from should be reported back to the user</para></listItem>
    /// </list>
    /// </exception>
    public class STFReader : IDisposable
	{
        /// <summary>Open a file, reader the header line, and prepare for STF parsing
        /// </summary>
        /// <param name="filename">Filename of the STF file to be opened and parsed.</param>
		public STFReader(string filename)
        {
            streamSTF = new StreamReader(filename, true); // was System.Text.Encoding.Unicode ); but I found some ASCII files, ie GLOBAL\SHAPES\milemarker.s
            FileName = filename;
            SIMISsignature = streamSTF.ReadLine();
            LineNumber = 2;
        }
        /// <summary>Use an open stream for STF parsing, this constructor assumes that the SIMIS signature has already been gathered (or there isn't one)
        /// </summary>
        /// <param name="inputStream">Stream that will be parsed.</param>
        /// <param name="fileName">Is only used for error reporting.</param>
        /// <param name="encoding">One of the Encoding formats, defined as static members in Encoding which return an Encoding type.  Eg. Encoding.ASCII or Encoding.Unicode</param>
        public STFReader(Stream inputStream, string fileName, Encoding encoding)
        {
            Debug.Assert(inputStream.CanSeek);
            FileName = fileName;
            streamSTF = new StreamReader(inputStream , encoding);
            LineNumber = 1;
        }

        /// <summary>Implements the IDisposable interface so this class can be implemented with the 'using(STFReader r = new STFReader(...)) {...}' C# statement.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~STFReader()
        {
            Dispose(false);
        }
        /// <summary>Releases the resources used by the STFReader.
        /// </summary>
        /// <param name="disposing">
        /// <para>true - release managed and unmanaged resources.</para>
        /// <para>false - release only unmanaged resources.</para>
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
#if DEBUG
            if (!IsEof(PeekPastWhitespace()))
                STFException.ReportWarning(this, "Some of this STF file was not parsed.");
#endif
            if (disposing)
            {
                streamSTF.Close(); streamSTF = null;
                itemBuilder.Length = 0;
                itemBuilder.Capacity = 0;
            }
        }

        /// <summary>Property that returns true when the EOF has been reached
        /// </summary>
        public bool EOF { get { return PeekChar() == -1; } }
        /// <summary>Filename property for the file being parsed - for reporting purposes
        /// </summary>
        public string FileName { get; private set; }
        /// <summary>Line Number property for the file being parsed - for reporting purposes
        /// </summary>
        public int LineNumber { get; private set; }
        /// <summary>SIMIS header read from the first line of the file being parsed
        /// </summary>
        public string SIMISsignature { get; private set; }
        /// <summary>Property returning the last {item} read using ReadItem() prefixed with string describing the nested block hierachy.
        /// <para>The string returned is formatted 'rootnode(nestednode(childnode(previous_item'.</para>
        /// </summary>
        /// <remarks>
        /// Tree is expensive method of reading STF files (especially for the GC) and should be avoided if possible.
        /// </remarks>
        public string Tree
        {
            get
            {
                if (tree_cache != null)
                    return tree_cache + previousItem;
                else
                {
                    StringBuilder sb = new StringBuilder(256);
                    foreach (string t in tree) sb.Append(t);
                    tree_cache = sb.ToString();
                    sb.Append(previousItem);
                    return sb.ToString();
                }
            }
        }

        /// <summary>This is the main function in STFReader, it returns the next whitespace delimited {item} from the STF file.
        /// </summary>
        /// <remarks>
        /// <alert class="important">If a comment/skip/#*/_* ignore block is the last {item} in a block, rather than being totally consumed a dummy '#' is returned, so if EndOFBlock() returns false, you always get an {item} (which can then just be ignored).</alert>
        /// </remarks>
        /// <returns>The next {item} from the STF file, any surrounding quotations will be not be returned.</returns>
        public string ReadItem()
        {
            #region If StepBackOneItem() has been called then return the previous output from ReadItem() rather than reading a new token
            if (stepbackoneitemFlag)
            {
                Debug.Assert(stepback.Item != null, "You must called at least one ReadItem() between StepBackOneItem() calls", "The current step back functionality only allows for a single step");
                string item = stepback.Item;
                previousItem = stepback.PrevItem;
                if (stepback.Tree != null) { tree = stepback.Tree; tree_cache = null; }
                stepbackoneitemFlag = false;
                stepback.Clear();
                return item;
            }
            #endregion
            return ReadItem(false);
        }
        /// <summary>Calling this function causes ReadItem() to repeat the last {item} that was read from the STF file
        /// </summary>
        /// <remarks>
        /// <para>The current implementation of StepBackOneItem() only allows for one "step back".</para>
        /// <para>This means that there each call to StepBackOneItem() must have an intervening call to ReadItem().</para>
        /// </remarks>
        public void StepBackOneItem()
        {
            Debug.Assert(stepback.Item != null, "You must called at least one ReadItem() between StepBackOneItem() calls", "The current step back functionality only allows for a single step");
            stepbackoneitemFlag = true;
        }

        /// <summary>Reports a critical error if the next {item} does not match the target.
        /// </summary>
        /// <param name="target">The next {item} contents we are expecting in the STF file.</param>
        /// <returns>The {item} read from the STF file</returns>
        public void MustMatch(string target)
        {
            if (EOF)
                throw new STFException(this, "Unexpected end of file");
            string s = ReadItem();
            if (s != target)
                throw new STFException(this, target + " Not Found - instead found " + s);
        }

        /// <summary>Returns true if the next character is the end of block, or end of file. Consuming the closing ")" all other values are not consumed.
        /// </summary>
        /// <remarks>
        /// <para>An STF block should be enclosed in parenthesis, ie ( {data_item} {data_item} )</para>
        /// </remarks>
        /// <returns>
        /// <para>true - An EOF, or closing parenthesis was found and consumed.</para>
        /// <para>false - Another type of {item} was found but not consumed.</para>
        /// </returns>
        public bool EndOfBlock()
        {
            if (stepbackoneitemFlag && (stepback.Item == ")"))
            {
                // Consume the step-back end-of-block
                stepbackoneitemFlag = false;
                stepback.Clear();
                return true;
            }
            int c = PeekPastWhitespace();
            if (c == ')')
            {
                c = streamSTF.Read();
                UpdateTreeAndStepBack(")");
            }
            return c == ')' || c == -1;
        }
        /// <summary>Read a block open (, and then consume the rest of the block without processing.
        /// If we find an immediate close ), then produce a warning, and return without consuming the parenthesis.
        /// </summary>
        public void SkipBlock()
		{
			string token = ReadItem(true);  // read the leading bracket ( 
            if (token == ")")   // just in case we are not where we think we are
            {
                STFException.ReportWarning(this, "Found a close parenthesis, rather than the expected block of data");
                StepBackOneItem();
                return;
            }
            else if (token != "(")
                throw new STFException(this, "SkipBlock() expected an open block but found a token instead: " + token);
            SkipRestOfBlock();
		}
        /// <summary>Skip to the end of this block, ignoring any nested blocks
        /// </summary>
        public void SkipRestOfBlock()
        {
            // We are inside a pair of brackets, skip the entire hierarchy to past the end bracket
            int depth = 1;
            while (!EOF && depth > 0)
            {
                string token = ReadItem(true);
                if (token == "(")
                    ++depth;
                if (token == ")")
                    --depth;
            }
        }

        /// <summary>Read an hexidecimal encoded number {constant_item}
        /// </summary>
        /// <param name="default_val">the default value if an unexpected ')' token is found</param>
        /// <returns>The next {constant_item} from the STF file.</returns>
        public uint ReadHex(uint? default_val)
        {
            string item = ReadItem();

            if ((default_val.HasValue) && (item == ")"))
            {
                STFException.ReportWarning(this, "When expecting a hex string, we found a ) marker. Using the default " + default_val.ToString());
                StepBackOneItem();
                return default_val.Value;
            }

            uint val;
            if (uint.TryParse(item, parseHex, parseNFI, out val)) return val;
            STFException.ReportWarning(this, "Cannot parse the constant hex string " + item);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an signed integer {constant_item}
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if an unexpected ')' token is found</param>
        /// <returns>The next {constant_item} from the STF file, with the suffix normalized to OR units.</returns>
        public int ReadInt(UNITS valid_units, int? default_val)
		{
            string token = ReadItem();

            if ((default_val.HasValue) && (token == ")"))
            {
                STFException.ReportWarning(this, "When expecting a number, we found a ) marker. Using the default " + default_val.ToString());
                StepBackOneItem();
                return default_val.Value;
            }

            int val;
            double scale = ParseUnitSuffix(ref token, valid_units);
            if (token.Length == 0) return 0;
            if (token[token.Length - 1] == ',') token = token.TrimEnd(',');
            if (int.TryParse(token, parseNum, parseNFI, out val)) return (scale == 1) ? val : (int)(scale * val);

            STFException.ReportWarning(this, "Cannot parse the constant number " + token);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an unsigned integer {constant_item}
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if an unexpected ')' token is found</param>
        /// <returns>The next {constant_item} from the STF file, with the suffix normalized to OR units.</returns>
        public uint ReadUInt(UNITS valid_units, uint? default_val)
		{
            string token = ReadItem();

            if ((default_val.HasValue) && (token == ")"))
            {
                STFException.ReportWarning(this, "When expecting a number, we found a ) marker. Using the default " + default_val.ToString());
                StepBackOneItem();
                return default_val.Value;
            }

            uint val;
            double scale = ParseUnitSuffix(ref token, valid_units);
            if (token.Length == 0) return 0;
            if (token[token.Length - 1] == ',') token = token.TrimEnd(',');
            if (uint.TryParse(token, parseNum, parseNFI, out val)) return (scale == 1) ? val : (uint)(scale * val);

            STFException.ReportWarning(this, "Cannot parse the constant number " + token);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an single precision floating point number {constant_item}
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if an unexpected ')' token is found</param>
        /// <returns>The next {constant_item} from the STF file, with the suffix normalized to OR units.</returns>
        public float ReadFloat(UNITS valid_units, float? default_val)
		{
            string token = ReadItem();

            if ((default_val.HasValue) && (token == ")"))
            {
                STFException.ReportWarning(this, "When expecting a number, we found a ) marker. Using the default " + default_val.ToString());
                StepBackOneItem();
                return default_val.Value;
            }

            float val;
            double scale = ParseUnitSuffix(ref token, valid_units);
            if (token.Length == 0) return 0.0f;
            if (token[token.Length - 1] == ',') token = token.TrimEnd(',');
            if (float.TryParse(token, parseNum, parseNFI, out val)) return (scale == 1) ? val : (float)(scale * val);

            STFException.ReportWarning(this, "Cannot parse the constant number " + token);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an double precision floating point number {constant_item}
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if an unexpected ')' token is found</param>
        /// <returns>The next {constant_item} from the STF file, with the suffix normalized to OR units.</returns>
        public double ReadDouble(UNITS valid_units, double? default_val)
		{
            string token = ReadItem();

            if ((default_val.HasValue) && (token == ")"))
            {
                STFException.ReportWarning(this, "When expecting a number, we found a ) marker. Using the default " + default_val.ToString());
                StepBackOneItem();
                return default_val.Value;
            }

            double val;
            double scale = ParseUnitSuffix(ref token, valid_units);
            if (token.Length == 0) return 0.0;
            if (token[token.Length - 1] == ',') token = token.TrimEnd(',');
            if (double.TryParse(token, parseNum, parseNFI, out val)) return scale * val;

            STFException.ReportWarning(this, "Cannot parse the constant number " + token);
            return default_val.GetValueOrDefault(0);
		}
        /// <summary>Enumeration limiting which units are valid when parsing a numeric constant.
        /// </summary>
        [Flags]
        public enum UNITS
        {
            /// <summary>No unit parsing is done on the {constant_item} - which is obviously fastest
            /// </summary>
            None = 0,
            /// <summary>Combined using an | with other UNITS if the unit is compulsary (compulsary units will slow parsing)
            /// </summary>
            Compulsary = 1 << 0,
            /// <summary>Valid Units: m, cm, mm, km, ft, ', in, "
            /// <para>Scaled to meters.</para>
            /// </summary>
            Distance = 1 << 1,
            /// <summary>Valid Units: m/s, mph, kph, kmh, km/h
            /// <para>Scaled to meters/second.</para>
            /// </summary>
            Speed = 1 << 2,
            /// <summary>Valid Units: kg, t, lb
            /// <para>Scaled to kilograms.</para>
            /// </summary>
            Weight = 1 << 3,
            /// <summary>Valid Units: n, kn, lbf
            /// <para>Scaled to newtons.</para>
            /// </summary>
            Force = 1 << 4,
            /// <summary>Valid Units: w, kw, hp
            /// <para>Scaled to watts.</para>
            /// </summary>
            Power = 1 << 5,
            /// <summary>Valid Units: n/m
            /// <para>Scaled to newtons/metre.</para>
            /// </summary>
            Stiffness = 1 << 6,
            /// <summary>Valid Units: n/m/s (+ '/m/s' in case the newtons is missed) 
            /// <para>Scaled to newtons/speed(m/s)</para>
            /// </summary>
            Resistance = 1 << 7,
            /// <summary>This is only provided for backwards compatibility - all new users should limit the units to appropriate types
            /// </summary>
            Any = -2
        }
        /// <summary>This function removes known unit suffixes, and returns a scaler to bring the constant into the standard OR units.
        /// </summary>
        /// <remarks>This function is marked internal so it can be used to support arithmetic processing once the elements are seperated (eg. 5*2m)
        /// </remarks>
        /// <param name="constant">string with suffix, after the function call the suffix is removed.</param>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <returns>The scaler that should be used to modify the constant to standard OR units.</returns>
        internal double ParseUnitSuffix(ref string constant, UNITS valid_units)
        {
            if (valid_units == UNITS.None)
                return 1;

            int beg, end, i;
            for (beg = end = i = 0; i < constant.Length; end = ++i)
            {
                char c = constant[i];
                if ((i == 0) && (c == '+')) { ++beg; continue; }
                if ((i == 0) && (c == '-')) continue;
                if ((c == '.') || (c == ',')) continue;
                if ((c == 'e') || (c == 'E') && (i < constant.Length - 1))
                {
                    c = constant[i + 1];
                    if ((c == '+') || (c == '-')) { ++i; continue; }
                }
                if ((c < '0') || (c > '9')) break;
            }
            if (i == constant.Length)
            {
                if ((valid_units & UNITS.Compulsary) > 0)
                    STFException.ReportWarning(this, "Missing a suffix for data expecting " + valid_units.ToString() + " units");
                return 1; // There is no suffix, it's all numeric
            }
            while ((i < constant.Length) && (constant[i] == ' ')) ++i; // skip the spaces

            string suffix = constant.Substring(i).ToLowerInvariant();
            constant = constant.Substring(beg, end - beg);
            if ((valid_units & UNITS.Distance) > 0)
                switch (suffix)
                {
                    case "m": return 1;
                    case "cm": return 0.01;
                    case "mm": return 0.001;
                    case "km": return 1e3;
                    case "ft": return 0.3048;
                    case "'": return 0.3048;
                    case "in": return 0.0254;
                    case "\"": return 0.0254;
                    case "in/2": return 0.0127; // This is a strange unit used to measure radius
                }
            if ((valid_units & UNITS.Speed) > 0)
                switch (suffix)
                {
                    case "m/s": return 1;
                    case "mph": return 0.44704;
                    case "kph": return 0.27778;
                    case "kmh": return 0.27778;
                    case "km/h": return 0.27778;
                }
            if ((valid_units & UNITS.Weight) > 0)
                switch (suffix)
                {
                    case "kg": return 1;
                    case "t": return 1e3;
                    case "lb": return 0.00045359237;
                }
            if ((valid_units & UNITS.Force) > 0)
                switch (suffix)
                {
                    case "n": return 1;
                    case "kn": return 1e3;
                    case "lbf": return 4.44822162;
                }
            if ((valid_units & UNITS.Power) > 0)
                switch (suffix)
                {
                    case "w": return 1;
                    case "kw": return 1e3;
                    case "hp": return 745.7;
                }
            if ((valid_units & UNITS.Stiffness) > 0)
                switch (suffix)
                {
                    case "n/m": return 1;
                }
            if ((valid_units & UNITS.Resistance) > 0)
                switch (suffix)
                {
                    case "n/m/s": return 1;
                    case "/m/s": return 1;
                }
            STFException.ReportWarning(this, "Found a suffix '" + suffix + "' which could not be parsed as a " + valid_units.ToString() + " unit");
            return 1;
        }


        /// <summary>Read an {item} from the STF format '( {item} ... )'
        /// </summary>
        /// <param name="default_val">the default value if the item is not found in the block.</param>
        /// <returns>The first item inside the STF block.</returns>
        public string ReadItemBlock(string default_val)
		{
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")" && (default_val != null))
            {
                StepBackOneItem();
                return default_val;
            }
            if (s == "(")
            {
                string result = ReadItem();
                SkipRestOfBlock();
                return result;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val;
		}
        /// <summary>Read an integer constant from the STF format '( {int_constant} ... )'
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if the constant is not found in the block.</param>
        /// <returns>The STF block with the first {item} converted to a integer constant.</returns>
        public int ReadIntBlock(UNITS valid_units, int? default_val)
		{
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")" && default_val.HasValue)
            {
                StepBackOneItem();
                return default_val.Value;
            }
            if (s == "(")
            {
                int result = ReadInt(valid_units, default_val);
                SkipRestOfBlock();
                return result;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an unsigned integer constant from the STF format '( {uint_constant} ... )'
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if the constant is not found in the block.</param>
        /// <returns>The STF block with the first {item} converted to a unsigned integer constant.</returns>
        public uint ReadUIntBlock(UNITS valid_units, uint? default_val)
        {
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")" && default_val.HasValue)
            {
                StepBackOneItem();
                return default_val.Value;
            }
            if (s == "(")
            {
                uint result = ReadUInt(valid_units, default_val);
                SkipRestOfBlock();
                return result;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an single precision constant from the STF format '( {float_constant} ... )'
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if the constant is not found in the block.</param>
        /// <returns>The STF block with the first {item} converted to a single precision constant.</returns>
        public float ReadFloatBlock(UNITS valid_units, float? default_val)
        {
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")" && default_val.HasValue)
            {
                StepBackOneItem();
                return default_val.Value;
            }
            if (s == "(")
            {
                float result = ReadFloat(valid_units, default_val);
                SkipRestOfBlock();
                return result;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val.GetValueOrDefault(0);
        }
        /// <summary>Read an double precision constant from the STF format '( {double_constant} ... )'
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">the default value if the constant is not found in the block.</param>
        /// <returns>The STF block with the first {item} converted to a double precision constant.</returns>
        public double ReadDoubleBlock(UNITS valid_units, double? default_val)
		{
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")" && default_val.HasValue)
            {
                StepBackOneItem();
                return default_val.Value;
            }
            if (s == "(")
            {
                double result = ReadDouble(valid_units, default_val);
                SkipRestOfBlock();
                return result;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val.GetValueOrDefault(0);
		}
        /// <summary>Reads the first item from a block in the STF format '( {double_constant} ... )' and return true if is not-zero or 'true'
        /// </summary>
        /// <param name="default_val">the default value if a item is not found in the block.</param>
        /// <returns><para>true - If the first {item} in the block is non-zero or 'true'.</para>
        /// <para>false - If the first {item} in the block is zero or 'false'.</para></returns>
        public bool ReadBoolBlock(bool default_val)
        {
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")")
            {
                StepBackOneItem();
                return default_val;
            }
            if (s == "(")
            {
                switch (s = ReadItem().ToLower())
                {
                    case "true": SkipRestOfBlock(); return true;
                    case "false": SkipRestOfBlock(); return false;
                    case ")": return default_val;
                    default:
                        int v;
                        if (int.TryParse(s, NumberStyles.Any, parseNFI, out v)) default_val = (v != 0);
                        SkipRestOfBlock();
                        return default_val;
                }
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val;
        }
        /// <summary>Read a Vector3 object in the STF format '( {X} {Y} {Z} ... )'
        /// </summary>
        /// <param name="valid_units">Any combination of the UNITS enumeration, to limit the availale suffixes to reasonable values.</param>
        /// <param name="default_val">The default vector if any of the values are not specified</param>
        /// <returns>The STF block as a Vector3</returns>
        public Vector3 ReadVector3Block(UNITS valid_units, Vector3 default_val)
        {
            if (EOF)
                STFException.ReportError(this, "Unexpected end of file");
            string s = ReadItem();
            if (s == ")")
            {
                StepBackOneItem();
                return default_val;
            }
            if (s == "(")
            {
                default_val.X = ReadFloat(valid_units, default_val.X);
                default_val.Y = ReadFloat(valid_units, default_val.Y);
                default_val.Z = ReadFloat(valid_units, default_val.Z);
                SkipRestOfBlock();
                return default_val;
            }
            STFException.ReportError(this, "Block Not Found - instead found " + s);
            return default_val;
        }

        /// <summary>The I/O stream for the STF file we are processing
        /// </summary>
        private StreamReader streamSTF;
        /// <summary>includeReader is used recursively in ReadItem() to handle the 'include' token, file include mechanism
        /// </summary>
        private STFReader includeReader = null;
        /// <summary>Remembers the last returned ReadItem().  If the next {item] is a '(', this is the block name used in the tree.
        /// </summary>
        private string previousItem = "";
        /// <summary>A list describing the hierachy of nested block tokens
        /// </summary>
        private List<string> tree = new List<string>();
        /// <summary>The tree cache is used to minimize the calls to StringBuilder when Tree is called repetively for the same hierachy.
        /// </summary>
        private string tree_cache;

        private static NumberStyles parseHex = NumberStyles.AllowLeadingWhite|NumberStyles.AllowHexSpecifier|NumberStyles.AllowTrailingWhite;
        private static NumberStyles parseNum = NumberStyles.AllowLeadingWhite | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowTrailingWhite;
        private static IFormatProvider parseNFI = NumberFormatInfo.InvariantInfo;
        #region *** StepBack Variables - It is important that all state variables in this STFReader class have a equivalent in the STEPBACK structure
        /// <summary>This flag is set in StepBackOneItem(), and causes ReadItem(), to use the stepback* variables to do an item repeat
        /// </summary>
        private bool stepbackoneitemFlag = false;
        /// <summary>Internal Structure used to group together the variables used to implement step back functionality.
        /// </summary>
        private struct STEPBACK
        {
            //streamSTF - is not needed for this stepback implementation
            //includeReader - is not needed for this stepback implementation
            /// <summary>The stepback* variables store the previous state, so StepBackOneItem() can jump back on {item}. stepbackItem � ReadItem() return
            /// </summary>
            public string Item;
            /// <summary>The stepback* variables store the previous state, so StepBackOneItem() can jump back on {item}. stepbackCurrItem � previousItem
            /// </summary>
            public string PrevItem;
            /// <summary>The stepback* variables store the previous state, so StepBackOneItem() can jump back on {item}. stepbackTree � tree
            /// <para>This item, is optimized, so when value is null it means stepbackTree was the same as Tree, so we don't create unneccessary memory duplicates of lists.</para>
            /// </summary>
            public List<string> Tree;
            //tree_cache can just be set to null, so it is re-evaluated from the stepback'd tree state variable if Tree is called
            /// <summary>Clear all of the members after a step back has been processed
            /// </summary>
            public void Clear()
            {
                Item = PrevItem = null;
                Tree = null;
            }
        };
        private STEPBACK stepback = new STEPBACK();
        #endregion

        #region *** Private Class Implementation
        private static bool IsWhiteSpace(int c) { return c >= 0 && c <= ' '; }
        private static bool IsEof(int c) { return c == -1; }
        private int PeekChar()
        {
            int c = streamSTF.Peek();
            if (IsEof(c))
            {
                // I've seen a problem with compressed input streams with a false -1 on peek
                c = streamSTF.Read();
                if (c != -1)
                    throw new InvalidDataException("Problem peeking eof in compressed file.");
            }
            return c;
        }
        private int PeekPastWhitespace()
        {
            int c = streamSTF.Peek();
            while (IsEof(c) || IsWhiteSpace(c)) // skip over eof and white space
            {
                c = ReadChar();
                if (IsEof(c))
                    break;   // break on reading eof 
                c = streamSTF.Peek();
            }
            return c;
        }
        private int ReadChar()
        {
            int c = streamSTF.Read();
            if (c == '\n') ++LineNumber;
            return c;
        }
        /// <summary>This is really a local variable in the function ReadItem(...) but it is a class member to stop unnecessary memory re-allocations.
        /// </summary>
        private StringBuilder itemBuilder = new StringBuilder(256);
        /// <summary>Internal Implementation - This is the main function that reads an item from the STF stream.
        /// </summary>
        /// <param name="skip_mode">True - we are in a skip function, and so we don't want to do any special token processing.</param>
        /// <returns>The next item from the STF file</returns>
        private string ReadItem(bool skip_mode)
        {
            #region If includeReader exists, then recurse down to get the next token from the included STF file
            if (includeReader != null)
            {
                string item = includeReader.ReadItem();
                UpdateTreeAndStepBack(item);
                if ((!includeReader.EOF) || (item.Length > 0)) return item;
                if (tree.Count != 0)
                    STFException.ReportWarning(includeReader, "Included file did not have a properly matched number of blocks.  It is unlikely the parent STF file will work properly.");
                includeReader.Dispose();
                includeReader = null;
            }
            #endregion

            int c;
            #region Skip past any leading whitespace characters
            for (; ; )
            {
                c = ReadChar();
                if (IsEof(c)) return UpdateTreeAndStepBack("");
                if (!IsWhiteSpace(c)) break;
            }
            #endregion

            itemBuilder.Length = 0;
            #region Handle Open and Close Block markers - parenthisis
            if (c == '(')
            {
                return UpdateTreeAndStepBack("(");
            }
            else if (c == ')')
            {
                return UpdateTreeAndStepBack(")");
            }
            #endregion
            #region Handle #&_ markers
            else if ((!skip_mode) && ((c == '#') || (c == '_')))
            {
                #region Move on to a whitespace so we can pick up any token starting with a #
                for (; ; )
                {
                    c = PeekChar();
                    if ((c == '(') || (c == ')')) break;
                    c = ReadChar();
                    if (IsEof(c))
                    {
                        STFException.ReportWarning(this, "Found a # marker immediately followed by an unexpected EOF.");
                        return UpdateTreeAndStepBack("");
                    }
                    if (IsWhiteSpace(c)) break;
                }
                #endregion
                #region Skip the comment item or block
                string comment = ReadItem();
                if (comment == "(") SkipRestOfBlock();
                #endregion
                string item = ReadItem();
                if (item == ")") { StepBackOneItem(); return "#"; }
                return item; // Now move on to the next token after the commented area
            }
            #endregion
            #region Build Quoted Items - including append operations
            else if (c == '"')
            {
                for (; ; )
                {
                    c = ReadChar();
                    if (IsEof(c))
                    {
                        STFException.ReportWarning(this, "Found an unexpected EOF, while reading an item started with a double-quote character.");
                        return UpdateTreeAndStepBack(itemBuilder.ToString());
                    }
                    if (c == '\\') // escape sequence
                    {
                        c = ReadChar();
                        if (c == 'n') itemBuilder.Append('\n');
                        else itemBuilder.Append((char)c);  // ie \, " etc
                    }
                    else if (c != '"')
                    {
                        itemBuilder.Append((char)c);
                    }
                    else //  end of quotation
                    {
                        // Anything other than a string extender now, means we have finished reading the item
                        if (PeekPastWhitespace() != '+') break;
                        ReadChar(); // Read the '+' character

                        #region Skip past any leading whitespace characters
                        for (; ; )
                        {
                            c = ReadChar();
                            if (IsEof(c))
                            {
                                STFException.ReportWarning(this, "Found an unexpected EOF, while reading an item started with a double-quote character and followed by the + operator.");
                                return UpdateTreeAndStepBack("");
                            }
                            if (!IsWhiteSpace(c)) break;
                        }
                        #endregion

                        if (c != '"')
                            throw new STFException(this, "Reading an item started with a double-quote character and followed by the + operator but then the next item must also be double-quoted.");

                    }
                }
            }
            #endregion
            #region Build Normal Items - whitespace delimitered
            else if (c != -1)
            {
                for (; ; )
                {
                    itemBuilder.Append((char)c);
                    c = PeekChar();
                    if ((c == '(') || (c == ')')) break;
                    c = ReadChar();
                    if (IsEof(c)) break;
                    if (IsWhiteSpace(c)) break;
                }
            }
            #endregion

            string result = itemBuilder.ToString();
            if (!skip_mode)
                switch (result.ToLower())
                {
                    #region Process special token - include
                    case "include":
                        string filename = ReadItem();
                        if (filename == "(")
                        {
                            filename = ReadItem();
                            SkipRestOfBlock();
                        }
                        if (tree.Count == 0)
                        {
                            includeReader = new STFReader(Path.GetDirectoryName(FileName) + @"\" + filename);
                            return ReadItem(); // Which will recurse down when includeReader is tested
                        }
                        else
                            throw new STFException(this, "Found an include directive, but it was enclosed in block parenthesis which is illegal.");
                    #endregion
                    #region Process special token - skip and comment
                    case "skip":
                        {
                            #region Skip the comment item or block
                            string comment = ReadItem();
                            if (comment == "(") SkipRestOfBlock();
                            #endregion
                            string item = ReadItem();
                            if (item == ")") { StepBackOneItem(); return "#"; }
                            return item; // Now move on to the next token after the commented area
                        }
                    case "comment":
                        {
                            #region Skip the comment item or block
                            string comment = ReadItem();
                            if (comment == "(") SkipRestOfBlock();
                            #endregion
                            string item = ReadItem();
                            if (item == ")") { StepBackOneItem(); return "#"; }
                            return item; // Now move on to the next token after the commented area
                        }
                    #endregion
                }

            return UpdateTreeAndStepBack(result);
        }
        /// <summary>Internal Implementation
        /// <para>This function is called by ReadItem() for every item read from the STF file (and Included files).</para>
        /// <para>If a block instuction is found, then tree list is updated.</para>
        /// <para>As this function is called once per ReadItem() is stores the previous value in stepback* variables (there is additional optimization that we only copy stepbackTree if the tree has changed.</para>
        /// <para>Now when the stepbackoneitemFlag flag is set, we use the stepback* copies, to move back exactly one item.</para>
        /// </summary>
        /// <param name="token"></param>
        private string UpdateTreeAndStepBack(string token)
        {
            stepback.Item = token;
            token = token.Trim();
            if (token == "(")
            {
                stepback.Tree = new List<string>(tree);
                stepback.PrevItem = previousItem;
                tree.Add(previousItem + "(");
                tree_cache = null; // The tree has changed, so we need to empty the cache which will be rebuilt if the property 'Tree' is used
                previousItem = "";
            }
            else if (token == ")")
            {
                stepback.Tree = new List<string>(tree);
                stepback.PrevItem = previousItem;
                if (tree.Count > 0)
                {
                    tree.RemoveAt(tree.Count - 1);
                    tree_cache = null; // The tree has changed, so we need to empty the cache which will be rebuilt if the property 'Tree' is used
                }
                previousItem = token;
            }
            else
            {
                stepback.Tree = null; // The tree has not changed so stepback doesn't need any data
                stepback.PrevItem = previousItem;
                previousItem = token;
            }
            return stepback.Item;
        }
        #endregion
	}

    public class STFException : Exception
    // STF errors display the last few lines of the STF file when reporting errors.
    {
        public static void ReportError(STFReader reader, string message)
        {
            Trace.TraceError("{2} in {0}:line {1}", reader.FileName, reader.LineNumber, message);
        }
        public static void ReportWarning(STFReader reader, string message)
        {
            Trace.TraceWarning("{2} in {0}:line {1}", reader.FileName, reader.LineNumber, message);
        }
        public static void ReportInformation(STFReader reader, Exception error)
        {
            Trace.TraceError("STF error in {0}:line {1}", reader.FileName, reader.LineNumber);
            Trace.WriteLine(error);
        }

        public STFException(STFReader reader, string message)
            : base(String.Format("{2} in {0}:line {1}", reader.FileName, reader.LineNumber, message))
        {
        }
    }
}
