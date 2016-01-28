/*=================================================
 *          ModelExporterInterpreter
 *  Copyright (C) 2011 Eigenvector Research, Inc.
 *=================================================
 * Interpreter for Model_Exporter output models for use in
 * C# (.NET) environments. Written by Eigenvector Research, Inc.
 * See:  http://www.eigenvector.com
 *
 * This file is provided as-is with no warranties. It may
 * be recompiled and distributed without restriction. If
 * this source code is distributed, this notice must remain
 * intact.
 *
 * This code exposes two classes:
 *  ModelInterpreter - the actual interpreter object
 *  Workspace        - a workspace object to manage variables
 *
 * The constructors for ModelInterpreter objects include two forms:
 *   new ModelInterpreter(String filename)  //read in XML in "filename"
 *   new ModelInterpreter(XmlDocument doc)  //use pre-read XML document "doc"
 *                    
 */
 
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using MatrixLibrary;

namespace EigenvectorInterpreter
{
    
    #region "Exceptions"
    public class EigenvectorInterpreterExceptions : ApplicationException
    { public EigenvectorInterpreterExceptions(string message) : base(message) { } }

    // The Exceptions in this Class
    class ValueNotSet : ApplicationException
    {
        public ValueNotSet(String name)
            :
            base("Value for the workspace variable \"" + name + "\" has not been set") { }
    }
    class WorkspaceCorrupted : ApplicationException
    {
        public WorkspaceCorrupted()
            :
            base("Workspace has been corrupted (variable name / value mismatch)") { }
    }
    class InputdataInfoNotFound : ApplicationException
    {
        public InputdataInfoNotFound()
            :
            base("Could not locate inputdata information in exported model") { }
    }
    class InputdataSizeNotFound : ApplicationException
    {
        public InputdataSizeNotFound()
            :
            base("Could not locate inputdata size information in exported model") { }
    }
    class InputdataSizeInvalid : ApplicationException
    {
        public InputdataSizeInvalid()
            :
            base("Number of variables (columns) in inputdata does not match number of variables expected by model") { }
    }
    
    class InformationNotFound : ApplicationException
    {
        public InformationNotFound()
            :
            base("Model Information tag not found in exported model") { }
    }

    class ModelTypeNotFound : ApplicationException
    {
        public ModelTypeNotFound()
            :
            base("Model type tag not found in exported model") { }
    }

    class InputDataTooManyRows : ApplicationException
    {
        public InputDataTooManyRows()
            :
            base("Inputdata may contain only one row") { }
    }
    class InputDataColumnMismatch : ApplicationException
    {
        public InputDataColumnMismatch()
            :
            base("Inputdata does not contain the correct number of variables (columns) for the model") { }
    }
    class InputDataMissing : ApplicationException
    {
        public InputDataMissing()
            :
            base("Inputdata has not been assigned prior to calling apply") { }
    }
    class ModelNotApplied : ApplicationException
    {
        public ModelNotApplied()
            :
            base("Apply method must be called before attempting to retrieve results") { }
    }
    class NoStepsFound : ApplicationException
    {
        public NoStepsFound()
            :
            base("No application steps could be found in model") { }
    }
    class UnparsableConstant : ApplicationException
    {
        public UnparsableConstant(String name, String stepDescription)
            :
            base("Unable to parse size or content for constant \"" + name + "\" in step \"" + stepDescription + "\"") { }
    }
    class WrongSizeConstant : ApplicationException
    { 
        public WrongSizeConstant(String name, String stepDescription)
            :
            base("Value parses to incorrect size for constant \"" + name + "\" in step \"" + stepDescription + "\"") {}
    }
    class ScriptMissing : ApplicationException
    {
        public ScriptMissing(String stepDescription)
            :
            base("Missing script for step \"" + stepDescription + "\"") { }
    }
    
    class UnrecognizedScriptCommand : ApplicationException
    {
        public UnrecognizedScriptCommand(String cmd)
            :
            base("Unrecognized script command \"" + cmd + "\"") { }
    }
    class BadScriptCommand : ApplicationException
    {
        public BadScriptCommand(String oneline)
            :
            base("Poorly formatted script command \"" + oneline + "\"") { }
    }
    #endregion

    //========================================================================================
    public class ModelInterpreter
    {
        //local results workspace and flag indicating if model has been applied
        private Boolean isApplied = false;
        private Workspace myWorkspace = new Workspace();

        //model information
        private List<ScriptStep> mySteps = new List<ScriptStep>();
        private Matrix myData = new Matrix(0,0);
        private Int32 myDataSize = 0;
        private XmlNode myInformation = null;
        private String myModelType = "";


        #region "Constructors"
        /// <summary>
        /// Create new ModelInterpreter object based on a string filename
        /// </summary>
        /// <param name="filename">Name of XML file to read containing Model_Exporter output</param>
        public ModelInterpreter(String filename)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(filename);
            preparse(xDoc);
        }

        /// <summary>
        /// Create new ModelInterpreter object based on existing XmlDocument object
        /// </summary>
        /// <param name="xDoc">XmlDocument containing Model_Exporter output</param>
        public ModelInterpreter(XmlDocument xDoc)
        {
            preparse(xDoc);
        }
        #endregion

        #region "Read-Only Properties"
        /// <summary>
        /// Return size of data expected for loaded model
        /// </summary>
        /// <returns>length (int) of vector of data expected for inputdata</returns>
        public int inputDataSize { get { return (myDataSize); } }

        /// <summary>
        /// Return information sub-field of Exported Model
        /// </summary>
        /// <returns></returns>
        public XmlNode information { get { return (myInformation); } }

        /// <summary>
        /// Returns modeltype of exported model
        /// </summary>
        public String modeltype { get { return (myModelType); } }

        /// <summary>
        /// Returns workspace of applied model
        /// </summary>
        /// <returns>Workspace after model application</returns>
        public Workspace results
        {
            get
            {
                if (!isApplied) throw new ModelNotApplied();
                return (myWorkspace);
            }
        }

        #endregion

        #region "Read/Write Properties"
        /// <summary>
        /// Assign inputdata to analyze
        /// </summary>
        /// <param name="indata">Row of data (type Matrix) to analyze</param>
        public Matrix inputdata
        {
            set
            {
                if (value.NoRows > 1) throw new InputDataTooManyRows();
                if (value.NoCols != inputDataSize) throw new InputDataColumnMismatch();
                //data is good, assign and clear workspace
                myWorkspace.clearAll();
                myData = value;
                isApplied = false;  //reset isApplied flag (indicating that model needs to be applied to this data)
            }
            get
            {
                return (myData);
            }
        }
        #endregion

        #region "Private Utility Methods"
        /// <summary>
        /// Get the size attribute from a node
        /// </summary>
        /// <param name="mynode">The XMLNode to get size attribute from</param>
        /// <returns>List of integers representing the size vector</returns>
        private List<Int32> getNodeSize(XmlNode mynode)
        {
            List<Int32> sz = new List<int>();
            String szStr = "";
            XmlAttributeCollection attrb = mynode.Attributes;
            XmlNode szNode = attrb.GetNamedItem("size");
            if (szNode == null) return (sz);

            szStr = szNode.InnerText;
            String delim = "[]";
            szStr = szStr.Trim(delim.ToCharArray());
            foreach (String item in szStr.Split(',')) sz.Add(Convert.ToInt32(item));
            return (sz);
        }

        /// <summary>
        /// Test a matrix for being a scalar matrix (single element)
        /// </summary>
        /// <param name="m">Matrix to examine</param>
        /// <returns>boolean TRUE if m is a scalar</returns>
        private Boolean isScalar(Matrix m) { return (m.NoCols == 1 & m.NoRows == 1); }

        /// <summary>
        /// Preparses the XML document for constants and scripts
        /// </summary>
        private void preparse(XmlDocument xDoc)
        {
            XmlDocument subnode = new XmlDocument();
            XmlNodeList nodeList;
            Int32 rows, cols;
            Int32 ri, ci;
            Matrix mItem = new Matrix(0, 0);
            String oneline;
            List<Int32> sz;
            String[] splt;
            ScriptStep stepInfo;

            //get expected data size
            myDataSize = 0;
            nodeList = xDoc.GetElementsByTagName("inputdata");
            if (nodeList.Count != 1) throw new InputdataInfoNotFound();  //no inputdata tag?
            foreach (XmlNode inputitem in nodeList.Item(0).ChildNodes) //parse inputdata tag children for "size" tag
            {
                if (inputitem.Name == "size")
                {
                    splt = inputitem.InnerText.Split(',');
                    if (splt.Length == 2) myDataSize = (Convert.ToInt32(splt[1]));   //locate number of columns
                    else throw new InputdataSizeInvalid();  //throw error if we get here and couldn't parse number of columns
                }
            }
            if (myDataSize==0) throw new InputdataSizeNotFound();  //throw error if we got here because we couldn't find inputdata "size"

            //get model information XmlNode
            nodeList = xDoc.GetElementsByTagName("information");
            if (nodeList.Count != 1) throw new InformationNotFound();  //no tag?
            myInformation = nodeList.Item(0);

            //get model type
            foreach (XmlNode onenode in myInformation.ChildNodes) if (onenode.Name == "modeltype") myModelType = onenode.InnerText;
            if (myModelType == "") throw new ModelTypeNotFound();

            //get all steps
            nodeList = xDoc.GetElementsByTagName("step");
            if (nodeList.Count == 0) throw new NoStepsFound();

            //create sorted list of steps
            List<XmlNode> steps = new List<XmlNode>();
            foreach (XmlNode onestep in nodeList)
            {
                foreach (XmlNode stepchild in onestep.ChildNodes)
                    if (stepchild.Name == "sequence")
                    {
                        steps.Insert(System.Convert.ToInt16(stepchild.InnerText) - 1, onestep);
                        break;
                    }
            }

            //cycle through steps
            foreach (XmlNode onestep in steps)
            {
                //initilize step information
                stepInfo = new ScriptStep();

                //get constants
                subnode.LoadXml(onestep.OuterXml);
                nodeList = subnode.GetElementsByTagName("description");
                if (nodeList.Count > 0) stepInfo.description = nodeList.Item(0).InnerText;
                else stepInfo.description = "unknown";

                stepInfo.constants = new Workspace();

                nodeList = subnode.GetElementsByTagName("constants");
                if (nodeList.Count > 0)
                {
                    foreach (XmlNode oneconstant in nodeList.Item(0).ChildNodes)
                    {
                        //get size and initialize matrix
                        sz = getNodeSize(oneconstant);
                        if (sz.Count <2 ) throw new UnparsableConstant(oneconstant.Name, stepInfo.description);
                        rows = sz[0];
                        cols = sz[1];

                        mItem = new Matrix(rows, cols);

                        //read in values and put into matrix
                        ci = 0;
                        ri = 0;
                        splt = oneconstant.InnerText.Split(new char[] { ',', ';' });
                        for (Int32 i = 0; i < splt.Length; i++)
                        {
                            ri = i / cols;
                            ci = i % cols;
                            mItem[ri, ci] = Convert.ToDouble(splt[i]);
                        }
                        if (ci != cols - 1 | ri != rows - 1) throw new WrongSizeConstant(oneconstant.Name, stepInfo.description);

                        //store in workspace
                        stepInfo.constants.setVar(oneconstant.Name, mItem);

                    }
                }

                //parse out individual commands and put into string list
                nodeList = subnode.GetElementsByTagName("script");
                if (nodeList.Count != 1) throw new ScriptMissing(stepInfo.description);

                foreach (XmlNode stepitem in nodeList.Item(0).ChildNodes)
                {
                    oneline = stepitem.InnerText.Trim();
                    splt = oneline.Split(new Char[] { ';' });
                    foreach (String onecmd in splt) if (onecmd.Trim().Length>0) stepInfo.script.Add(onecmd.Trim());

                }

                //Got everything for this step, store in step object
                mySteps.Add(stepInfo);
            }
        }
        #endregion

        #region "Public Methods"
        /// <summary>
        /// Apply model to inputdate
        /// </summary>
        public void apply()
        {
            if (isApplied) return;  //no error, we just don't need to do anything!
            if (myData.NoRows == 0) throw new InputDataMissing();
            //not yet applied

            //copy myData into "x" in workspace
            myWorkspace.clearAll();
            myWorkspace.setVar("x", myData);

            //cycle through steps
            Int32 ri, ci;
            Matrix mItem = new Matrix(0, 0);
            String outvarname, fnname, invar1name, invar2name;
            Matrix invar1, invar2, outvar;
            Int32 rows1, cols1, rows2, cols2;
            String[] splt;

            foreach (ScriptStep onestep in mySteps)
            {
                //get constants
                myWorkspace.setVar(onestep.constants);  //copy constants to real workspace

                //parse script string
                outvar = new Matrix(0, 0);
                foreach (String oneline in onestep.script)
                {
                    //parse a line which has format:
                    //      out = fnname(invar1name,invar2name);
                    // or:  out = fnname(invar1name);

                    splt = oneline.Split(new Char[] { '=' }, (Int32)2); //outvarname = fnname(invar1name,invar2name);
                    outvarname = splt[0].Trim();
                    if (splt.Length < 2) throw new BadScriptCommand(oneline);

                    splt = splt[1].Split(new Char[] { '(' }, (Int32)2);   //fnname(invar1name,invar2name);
                    fnname = splt[0].Trim();
                    if (splt.Length < 2) throw new BadScriptCommand(oneline);

                    splt = splt[1].Split(new Char[] { ')' }, (Int32)2);   //invar1name,invar2name);

                    splt = splt[0].Split(',');   //invar1name,invar2name
                    invar1name = splt[0].Trim();
                    if (splt.Length > 1) invar2name = splt[1].Trim(); else invar2name = "";

                    //got the parts, do the math
                    invar1 = myWorkspace.getVar(invar1name);
                    if (invar2name != "") invar2 = myWorkspace.getVar(invar2name); else invar2 = new Matrix(0, 0);
                    
                    rows1 = invar1.NoRows;
                    cols1 = invar1.NoCols;
                    rows2 = invar2.NoRows;
                    cols2 = invar2.NoCols;

                    switch (fnname.ToLower())
                    {
                        /*
                         Single Input Functions
                        C = function(A);  
                         abs             Absolute Value     Removal of sign of elements
                         log10           log (base 10)      Base 10 logarithm of elements
                         transpose       transpose array    Exchange rows for columns ( ' )
                        */
                        case "abs":
                            outvar = new Matrix(rows1,cols1);
                            for (ci = 0; ci < cols1; ci++)
                                for (ri = 0; ri < rows1; ri++)
                                    outvar[ri, ci] = Math.Abs(invar1[ri, ci]);
                            break;
                        case "log10":
                            outvar = new Matrix(rows1, cols1);
                            for (ci = 0; ci < cols1; ci++)
                                for (ri = 0; ri < rows1; ri++)
                                    outvar[ri, ci] = Math.Log10(invar1[ri, ci]);
                            break;
                        case "transpose":
                            outvar = Matrix.Transpose(invar1);
                            break;

                        /*
                        Double Input Functions
                        C = function(A,B);
                           plus          Plus                              Addition of paired elements (+)
                           minus         Minus                             Subtraction of paired elements (-)
                           mtimes        Matrix multiply (dot product)     Dot product of matrices (*)
                           times         Array multiply                    Multiplication of paired elements (.*)
                           power         Array power                       Exponent using paired elements (.^)
                           rdivide       Right array divide                 Division of paired elements (./)
                        */

                        case "plus":
                            if (!isScalar(invar1) && !isScalar(invar2))
                            {
                                // with two matricies
                                if (cols2 != cols1 || rows2 != rows1) throw new MatrixDimensionException();
                                outvar = invar1 + invar2;
                            }
                            else if (!isScalar(invar1))
                            {
                                // with scalar var2, matrix var1
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] + invar2[0, 0];
                            }
                            else
                            {
                                // with scalar var1, matrix var2 (or two scalars)
                                outvar = new Matrix(rows2, cols2);
                                for (ci = 0; ci < cols2; ci++)
                                    for (ri = 0; ri < rows2; ri++)
                                        outvar[ri, ci] = invar1[0, 0] + invar2[ri, ci];
                            }
                            break;

                        case "minus":
                            if (!isScalar(invar1) && !isScalar(invar2))
                            {
                                // with two matricies
                                if (cols2 != cols1 || rows2 != rows1) throw new MatrixDimensionException();
                                outvar = invar1 - invar2;
                            }
                            else if (!isScalar(invar1))
                            {
                                // with scalar var2, matrix var1
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] - invar2[0, 0];
                            }
                            else
                            {
                                // with scalar var1, matrix var2 (or two scalars)
                                outvar = new Matrix(rows2, cols2);
                                for (ci = 0; ci < cols2; ci++)
                                    for (ri = 0; ri < rows2; ri++)
                                        outvar[ri, ci] = invar1[0, 0] - invar2[ri, ci];
                            }
                            break;

                        case "mtimes":
                            if (isScalar(invar1)) outvar = invar1[0,0] * invar2;
                            else if (isScalar(invar2)) outvar = invar1 * invar2[0,0];
                            else outvar = invar1 * invar2;
                            break;

                        case "times":
                            if (!isScalar(invar1) && !isScalar(invar2))
                            {
                                // with two matricies
                                if (cols2 != cols1 || rows2 != rows1) throw new MatrixDimensionException();
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] * invar2[ri, ci];
                            }
                            else if (!isScalar(invar1))
                            {
                                // with scalar var2, matrix var1
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] * invar2[0, 0];
                            }
                            else
                            {
                                // with scalar var1, matrix var2 (or two scalars)
                                outvar = new Matrix(rows2, cols2);
                                for (ci = 0; ci < cols2; ci++)
                                    for (ri = 0; ri < rows2; ri++)
                                        outvar[ri, ci] = invar1[0, 0] * invar2[ri, ci];
                            }
                            break;

                        case "rdivide":
                            if (!isScalar(invar1) && !isScalar(invar2))
                            {
                                // with two matricies
                                if (cols2 != cols1 || rows2 != rows1) throw new MatrixDimensionException();
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] / invar2[ri, ci];
                            }
                            else if (!isScalar(invar1))
                            {
                                // with scalar var2, matrix var1
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = invar1[ri, ci] / invar2[0, 0];
                            }
                            else
                            {
                                // with scalar var1, matrix var2 (or two scalars)
                                outvar = new Matrix(rows2, cols2);
                                for (ci = 0; ci < cols2; ci++)
                                    for (ri = 0; ri < rows2; ri++)
                                        outvar[ri, ci] = invar1[0, 0] / invar2[ri, ci];
                            }
                            break;

                        case "power":
                            if (!isScalar(invar2) & !isScalar(invar1))
                            {
                                // with two matricies
                                if (cols2 != cols1 | rows2 != rows1) throw new MatrixDimensionException();
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = Math.Pow(invar1[ri, ci], invar2[ri, ci]);
                            }
                            else if (!isScalar(invar2) & isScalar(invar1))
                            {
                                // with scalar base, matrix power
                                outvar = new Matrix(rows2, cols2);
                                for (ci = 0; ci < cols2; ci++)
                                    for (ri = 0; ri < rows2; ri++)
                                        outvar[ri, ci] = Math.Pow(invar1[0, 0], invar2[ri, ci]);
                            }
                            else 
                            {
                                // with scalar power, matrix base (or two scalars)
                                outvar = new Matrix(rows1, cols1);
                                for (ci = 0; ci < cols1; ci++)
                                    for (ri = 0; ri < rows1; ri++)
                                        outvar[ri, ci] = Math.Pow(invar1[ri, ci], invar2[0, 0]);
                            }
                            break;

                        case "cols":
                            if (rows2 != 1) throw new MatrixDimensionException();
                            outvar = new Matrix(rows1, cols2);
                            for (ci = 0; ci < cols2; ci++)
                                for (ri = 0; ri < rows1; ri++)
                                    outvar[ri, ci] = invar1[ri, Convert.ToInt32(invar2[0, ci])-1];
                            break;

                        case "rows":
                            if (rows2 != 1) throw new MatrixDimensionException();
                            outvar = new Matrix(cols2, cols1);
                            for (ci = 0; ci < cols1; ci++)
                                for (ri = 0; ri < cols2; ri++)
                                    outvar[ri, ci] = invar1[Convert.ToInt32(invar2[0, ri]) - 1, ci];
                            break;

                        default:
                            throw new UnrecognizedScriptCommand(fnname);

                    }

                    //store result into workspace
                    myWorkspace.setVar(outvarname, outvar);

                
                }



            }

            //indicate model was applied and exit
            isApplied = true;
        }
        #endregion

    }

    //========================================================================================
    /// <summary>
    /// Internal class used to hold the parts of one step
    /// </summary>
    internal class ScriptStep
    {
        private List<String> myScript = new List<string>();
        private String myDescription = "";
        private Workspace myConstants = new Workspace();

        public ScriptStep() { }

        public List<String> script { set { myScript = value; } get { return (myScript); } }
        public String description { set { myDescription = value; } get { return (myDescription); } }
        public Workspace constants { set { myConstants = value; } get { return (myConstants); } }

    }

    //========================================================================================

    /// <summary>
    /// Class used to hold a "workspace" consisting of matrices with assigned names.
    /// </summary>
    public class Workspace
    {
        private List<String> varNames = new List<String>();
        private List<Matrix> varValues = new List<Matrix>();

        /// <summary>
        /// Named-variable storage class to store Model_Exporter workspace variables
        /// </summary>
        public Workspace()
        {
            clearAll();
        }

        /// <summary>
        /// Return a sorted list of all the variables in the workspace
        /// </summary>
        public List<String> varList {
            get
            {
                List<String> mlist = new List<string>(varNames);
                mlist.Sort();
                return (mlist);
            }
        }

        /// <summary>
        /// Set the value of a specific workspace variable
        /// </summary>
        /// <param name="name">string name of the variable to set</param>
        /// <param name="value">value (as Matrix) of the variable to set</param>
        public void setVar(String name, Matrix value)
        {
            if (isSet(name))
            {
                //its there, replace existing
                varValues[getVarIndex(name)] = value;
            }
            else
            {
                //not there, add to end
                varNames.Add(name);
                varValues.Add(value);
                if (varNames.Count != varValues.Count) throw new WorkspaceCorrupted();
            }

            
        }
        /// <summary>
        /// Set variables in workspace by copying all items from another workspace (duplicate variable names overwritten)
        /// </summary>
        /// <param name="toadd">workspace object to copy from</param>
        public void setVar(Workspace toadd)
        {
            foreach (String item in toadd.varList) setVar(item,toadd.getVar(item));
        }

        /// <summary>
        /// Get a value of a specific workspace variable
        /// </summary>
        /// <param name="name">string name of the variable to retreive</param>
        /// <returns></returns>
        public Matrix getVar(String name) 
        {
            if (isSet(name)) return varValues[getVarIndex(name)];
            else throw new ValueNotSet(name);  //not there?? throw exception

        }
        /// <summary>
        /// Determine if a specific variable (specified by name) is present in the workspace
        /// </summary>
        /// <param name="name">string name of the variable to locate</param>
        /// <returns></returns>
        public Boolean isSet(String name) { return (getVarIndex(name)>-1); }

        /// <summary>
        /// Clears all variables in workspace
        /// </summary>
        public void clearAll()
        {
            varNames.Clear();
            varValues.Clear();
        }

        /// <summary>
        /// Internal function used to locate the index of the named variable in the array
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private int getVarIndex(String name)
        {
            int pos = varNames.FindIndex(delegate(String item) { return name.ToLower() == item.ToLower(); });
            return (pos);
        }

    }


    //========================================================================================
    /// <summary>
    /// test startup class
    /// </summary>
    static class startupClass
    {
        public static void Main(String[] instr)
        {
            DateTime startTime = DateTime.Now;            //start timer
            
            //create instance of a ModelInterpreter object using the specified file
            //could also pass an XmlDocument here (if the XML was stored in a database
            //instead of a file, for example)
            ModelInterpreter test = new ModelInterpreter("plsexample.xml");

            //display some of the common model information
            Console.WriteLine("Model type: " + test.modeltype);
            Console.WriteLine("Expected Data Size:" + test.inputDataSize);
            
            //how long did it take to parse the XML?
            Console.WriteLine("Preparse Time:" + (DateTime.Now - startTime));

            //create data to pass to the model
            Matrix inmatrix = new Matrix(1, test.inputDataSize);
            for (int i = 0; i < inmatrix.NoCols; i++) { inmatrix[0, i] = i + 1; };
            test.inputdata = inmatrix;

            startTime = DateTime.Now;            //start timer

            //set data and apply model            
            test.inputdata = inmatrix;     //assign input data to object
            test.apply();  //apply model
            
            //how long did it take to apply the model?
            Console.WriteLine("Elapsed Time:" + (DateTime.Now - startTime));

            //Typical outputs for a PLS model:
            /*
            Console.WriteLine("yhat = " + test.results.getVar("yhat"));
            Console.WriteLine("T2 = " + test.results.getVar("T2"));
            Console.WriteLine("Q = " + test.results.getVar("Q"));
            /* */
                 
            //List ALL contents of workspace       
            /* */
            foreach (String name in test.results.varList)
            {
                Console.WriteLine(name + " = \n" + test.results.getVar(name));
            }
            /* */
            Console.WriteLine("Press Enter to end test...");
            Console.Read();

        }
    }

}
