/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections;
using System.IO;

namespace MSTS
{
    /// <summary>
    /// Work with consist files
    /// </summary>
    public class CONFile
    {
        public string FileName;   // no extension, no path
        public string Description;  // form the Name field or label field of the consist file
        public Train_Config Train;
        public CONFile(string filenamewithpath)
        {
            FileName = Path.GetFileNameWithoutExtension(filenamewithpath);
            Description = FileName;
            using (STFReader stf = new STFReader(filenamewithpath, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("train", ()=>{ Train = new Train_Config(stf); }),
                });
        }
    }
}

