package evri.eigenvectorinterpreter;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;

import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.Node;
import org.w3c.dom.NodeList;

import Jama.Matrix;

public class ModelInterpreter {
	//local results workspace and flag indicating if model has been applied
	private boolean isApplied = false;
	private Workspace myWorkspace = new Workspace();

	// model information
	private List<ScriptStep> mySteps = new ArrayList<ScriptStep>();
	private Matrix myData = new Matrix(0,0);
	private int myDataSize = 0;
	//	private Node myInformation = null;
	private String myModelType = "";

	// read-only
	String modeltype;
	Document information;

	// "filename" Name of XML file to read containing Model_Exporter output
	public ModelInterpreter(String filename) throws Exception {
		Document xDoc = loadXml(filename);
		preparse(xDoc);
	}

	// XML Document containing Model_Exporter output
	public ModelInterpreter(Document xdoc) throws Exception {
		preparse(xdoc);
	}

	public void apply() throws InputDataMissing, BadScriptCommand, MatrixDimensionException, UnknownFunctionException {
		//
		if (isApplied) return;  //no error, we just don't need to do anything!

		//not yet applied
		if (myData.getRowDimension() == 0) {
			throw new InputDataMissing();	
		}

		//copy myData into "x" in workspace
		myWorkspace.clearAll();
		myWorkspace.setVar("x", myData);        

		//cycle through steps
		int ri, ci;
//		Matrix mItem = new Matrix(0, 0);
		String outvarname, fnname, invar1name, invar2name;
		Matrix invar1, invar2, outvar;
		int rows1, cols1, rows2, cols2;
		String[] splt;

		int nsteps = mySteps.size();
		for(int i=0; i<nsteps; i++)
		{
			ScriptStep onestep = mySteps.get(i);
			//get constants
			myWorkspace.setVar(onestep.getMyConstants());  //copy constants to real workspace

			//parse script string
			outvar = new Matrix(0, 0);
			for(String oneline : onestep.getMyScript())
			{
				//parse a line which has format:
				//      out = fnname(invar1name,invar2name);
				// or:  out = fnname(invar1name);

				splt = oneline.split("[=]");
				outvarname = splt[0].trim();
				if( splt.length<2 ) {
					throw new BadScriptCommand(oneline);
				}

				splt = splt[1].split("[(]");
				fnname = splt[0].trim();
				if(splt.length<2) {
					throw new BadScriptCommand(oneline);
				}

				splt = splt[1].split("[)]");   //invar1name,invar2name);
				splt = splt[0].split("[,]");   //invar1name,invar2name
				invar1name = splt[0].trim();
				if(splt.length > 1) {
					invar2name = splt[1].trim(); 
				} else {
					invar2name = "";
				}

				//got the parts, do the math
				invar1 = myWorkspace.getVar(invar1name);
				if (invar2name.compareTo("")!=0) {
					invar2 = myWorkspace.getVar(invar2name); 
				} else {
					invar2 = new Matrix(0, 0);
				}

				rows1 = invar1.getRowDimension();
				cols1 = invar1.getColumnDimension();
				rows2 = invar2.getRowDimension();
				cols2 = invar2.getColumnDimension();

				String fnnamelc = fnname.toLowerCase();
				/*
                     Single Input Functions
                     C = function(A);  
                     abs             Absolute Value     Removal of sign of elements
                     log10           log (base 10)      Base 10 logarithm of elements
                     transpose       transpose array    Exchange rows for columns ( ' )
				 */
				if(fnnamelc.equals("abs")) {
					outvar = new Matrix(rows1,cols1);
					for (ci = 0; ci < cols1; ci++) {
						for (ri = 0; ri < rows1; ri++) {
							outvar.set(ri, ci, Math.abs(invar1.get(ri, ci))); 
						}
					}
				} else if(fnnamelc.equals("log10")) {
					outvar = new Matrix(rows1,cols1);
					for (ci = 0; ci < cols1; ci++) {
						for (ri = 0; ri < rows1; ri++) {
							outvar.set(ri, ci, Math.log10(invar1.get(ri, ci))); 
						}
					}
				} else if(fnnamelc.equals("transpose")) {
					outvar = invar1.transpose();

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

				} else if(fnnamelc.equals("plus")) {
					if (!isScalar(invar1) && !isScalar(invar2))
					{
						// with two matrices
						outvar = invar1.plus(invar2);
					} else if (!isScalar(invar1))
					{
						// with scalar var2, matrix var1
						outvar = new Matrix(rows1, cols1);
						double scalar2 = invar2.get(0, 0);
						for (ci = 0; ci < cols1; ci++) {
							for (ri = 0; ri < rows1; ri++) {
								outvar.set(ri, ci, (invar1.get(ri, ci) + scalar2));
							}
						}
					}
					else
					{
						// with scalar var1, matrix var2 (or two scalars)
						outvar = new Matrix(rows2, cols2);
						double scalar1 = invar1.get(0, 0);
						for (ci = 0; ci < cols2; ci++) {
							for (ri = 0; ri < rows2; ri++) {
								outvar.set(ri, ci, (invar2.get(ri, ci) + scalar1));
							}
						}
					}
				} else if(fnnamelc.equals("minus")) {
					if (!isScalar(invar1) && !isScalar(invar2))
					{
						// with two matrices
						outvar = invar1.minus(invar2);
					} else if (!isScalar(invar1))
					{
						// with scalar var2, matrix var1
						outvar = new Matrix(rows1, cols1);
						double scalar2 = invar2.get(0, 0);
						for (ci = 0; ci < cols1; ci++) {
							for (ri = 0; ri < rows1; ri++) {
								outvar.set(ri, ci, (invar1.get(ri, ci) - scalar2));
							}
						}
					}
					else
					{
						// with scalar var1, matrix var2 (or two scalars)
						outvar = new Matrix(rows2, cols2);
						double scalar1 = invar1.get(0, 0);
						for (ci = 0; ci < cols2; ci++) {
							for (ri = 0; ri < rows2; ri++) {
								outvar.set(ri, ci, (scalar1 - invar2.get(ri, ci)));
							}
						}
					}

				} else if(fnnamelc.equals("mtimes")) {
					// matrix product
					if (!isScalar(invar1) && !isScalar(invar2)) {   
						outvar = invar1.times(invar2); 
					} else if(!isScalar(invar1) & isScalar(invar2)) {
						outvar = invar1.times( invar2.get(0, 0));
					} else {
						outvar = invar2.times(invar1.get(0, 0));
					}                	               		

				} else if(fnnamelc.equals("times")) {
					// element pairwise product
					if (!isScalar(invar1) && !isScalar(invar2)) {  
						outvar = invar1.arrayTimes(invar2);
					} else if (!isScalar(invar1)) {   
						// matrix var1, scalar var2, 
						outvar = invar1.times(invar2.get(0, 0));
					} else {
						// scalar var1, matrix or scalar var2
						outvar = invar2.times(invar1.get(0, 0));        // *** CHECK
					}
				}  else if(fnnamelc.equals("rdivide")) {
					// Right array divide                 Division of paired elements (./)
					if (!isScalar(invar1) && !isScalar(invar2)) {   
						// with two matrices
						outvar = invar1.arrayRightDivide(invar2);
					} else if (!isScalar(invar1)) {   
						// matrix var1, scalar var2, 
						outvar = invar1.times(1/invar2.get(0, 0));
					} else  {   
						// scalar var1, matrix or scalar var2,            *** CHECK: is there a nicer way? ***
						outvar = invar2.arrayRightDivide(invar2).arrayRightDivide(invar2).times(invar1.get(0, 0));
					}                 	
				}  else if(fnnamelc.equals("power")) {
					// Array power                       Exponent using paired elements (.^)
					// with scalar var2, matrix var1
					outvar = new Matrix(rows1, cols1);
					if (!isScalar(invar1) && !isScalar(invar2)) {   
						// with two matrices - must be same dims
						outvar = new Matrix(rows1, cols1);
						for (ci = 0; ci < cols1; ci++) {
							for (ri = 0; ri < rows1; ri++) {
								outvar.set(ri, ci, Math.pow(invar1.get(ri, ci), invar2.get(ri,  ci)));
							}
						}
					} else if (!isScalar(invar1)) {
						// scalar var2, matrix var1
						outvar = new Matrix(rows1, cols1);
						double scalar2 = invar2.get(0, 0);
						for (ci = 0; ci < cols1; ci++) {
							for (ri = 0; ri < rows1; ri++) {
								outvar.set(ri, ci, Math.pow(invar1.get(ri, ci), scalar2));
							}
						}
					} else {
						// scalar var1, matrix var2, or both scalars
						outvar = new Matrix(rows2, cols2);
						double scalar1 = invar1.get(0, 0);
						for (ci = 0; ci < cols2; ci++) {
							for (ri = 0; ri < rows2; ri++) {
								outvar.set(ri, ci, Math.pow(scalar1, invar2.get(ri, ci)));
							}
						}
					}                 
				} else if(fnnamelc.equals("cols")) {
					// Index (1-based) into columns of matrix      Select or replicate columns  ( A(:,B) )                           
					if (rows2 != 1) throw new MatrixDimensionException();
					outvar = new Matrix(rows1, cols2);
					for (ci = 0; ci < cols2; ci++) {
						for (ri = 0; ri < rows1; ri++) {
							outvar.set(ri, ci, invar1.get(ri, (int)(invar2.get(0, ci)-1)));
						}
					}

				} else if(fnnamelc.equals("rows")) {
					// Index (1-based) into rows of matrix         Select or replicate rows     ( A(B,:) )
					if (rows2 != 1) throw new MatrixDimensionException();
					outvar = new Matrix(cols2, cols1);
					for (ci = 0; ci < cols1; ci++) {
						for (ri = 0; ri < cols2; ri++) {
							outvar.set(ri, ci, invar1.get((int)(invar2.get(0, ci)-1), ci));
						}
					}                	               		

				} else {
					throw new UnknownFunctionException(fnname);  
				}

				//store result into workspace
				myWorkspace.setVar(outvarname, outvar);
			}
		}

		//indicate model was applied and exit
		isApplied = true;     
	}

	public Workspace getMyWorkspace() {       
		return myWorkspace;
	}	

	/*
	 * Returns workspace of applied model
	 */
	public Workspace getResults() throws ModelNotApplied
	{       
		if(!isApplied) {
			throw new ModelNotApplied();
		}
		return (myWorkspace);      
	}

	private Document loadXml(String filename) throws Exception {
		Document doc = null;
		File file = new File(filename);
		DocumentBuilderFactory dbf = DocumentBuilderFactory.newInstance();
		DocumentBuilder db = dbf.newDocumentBuilder();
		doc = db.parse(file);
		return doc;
	}

	private void preparse(Document doc) {
		NodeList nodeList = null;
		int rows, cols;
		Matrix mItem = new Matrix(0, 0);
		//		String oneline;
		String[] splt = null;
		ScriptStep stepInfo = null;
		//		List<Integer> sz = new ArrayList<Integer>();
		try {
			//get expected data size
			nodeList = doc.getElementsByTagName("inputdata");
			if( nodeList.getLength()  != 1) {
				throw new InputdataInfoNotFoundException();         //no inputdata tag?
			}	

			NodeList childNodes = nodeList.item(0).getChildNodes();
			for (int s = 0; s < childNodes.getLength(); s++) {      //parse inputdata tag children for "size" tag
				Node inputItem = childNodes.item(s);
				if( inputItem.getNodeName().equals("size")) {
					splt = inputItem.getTextContent().split(",");
					if (splt.length == 2) {
						myDataSize = Integer.parseInt(splt[1]);     //locate number of columns  
					} else {
						throw new InputdataSizeInvalidException();  //throw error if we get here and couldn't parse number of columns
					}
					if (myDataSize==0) {
						throw new InputdataSizeNotFoundException();  //throw error if we got here because we couldn't find inputdata "size"
					}
					break;
				}
			}

			//get model information Node
			nodeList = doc.getElementsByTagName("information");
			if( nodeList.getLength()  != 1) {
				throw new InformationNotFoundException();         //no inputdata tag?
			}			
			//			myInformation = nodeList.item(0);
			childNodes = nodeList.item(0).getChildNodes();
			//get model type
			for (int s = 0; s < childNodes.getLength(); s++) {      //parse myInformation tag children for "modeltype" tag
				Node inputItem = childNodes.item(s);
				if( inputItem.getNodeName().equals("modeltype")) {
					myModelType = inputItem.getTextContent();
					if( myModelType.length() == 0) {						
						throw new Exception("ModelType not found");
					}
					break;
				}
			}

			//get all steps
			nodeList = doc.getElementsByTagName("step");
			if (nodeList.getLength() == 0) throw new NoStepsFoundException();

			//create sorted list of steps
			List<Node> steps = new ArrayList<Node>();
			//get model type
			for (int s = 0; s < nodeList.getLength(); s++) {      //loop steps, find each "sequence" tag
				Node onestep = nodeList.item(s);
				for( int sc = 0; sc < onestep.getChildNodes().getLength(); sc++) {					
					Node stepchild = onestep.getChildNodes().item(sc);
					if( stepchild.getNodeName().equals("sequence")) {
						steps.add(Integer.parseInt(stepchild.getTextContent()) - 1, onestep);
						break;
					}
				}
			}

			//cycle through steps		
			for (Node onestep : steps)
			{
				//initilize step information
				stepInfo = new ScriptStep();

				if (onestep.getNodeType() == Node.ELEMENT_NODE) {
					Element step = (Element) onestep;
					NodeList descriptionElemLst = step.getElementsByTagName("description");
					if(descriptionElemLst.getLength()>0) {
						Element descriptionElem = (Element) descriptionElemLst.item(0);
						String descript = descriptionElem.getTextContent();
						stepInfo.setMyDescription(descript);
					} else {
						stepInfo.setMyDescription("unknown");
					}

					// constants
					NodeList constantsElemLst = step.getElementsByTagName("constants");
					Element constantsElem = (Element) constantsElemLst.item(0);
					constantsElemLst = constantsElem.getChildNodes();				
					Workspace workspace = new Workspace();
					//	Get the constants
					for (int ix=0; ix< constantsElemLst.getLength(); ix++) {
						Node oneconstant = constantsElemLst.item(ix);
						String nodetext = oneconstant.getTextContent();							
						if(oneconstant instanceof Element){
							//a child element to process
							Element child = (Element) oneconstant;
							int[] dims = getNodeSize(child);
							if( dims==null | dims.length<2 ) {
								throw new UnparsableConstant(oneconstant.getNodeName(), stepInfo.getMyDescription());
							}

							rows = dims[0];
							cols = dims[1];
							mItem = new Matrix(rows, cols);

							String[] vals = nodetext.split("[,;]");
							int ci = 0;
							int ri = 0;
							for(int i=0; i<vals.length; i++) {
								//								ri = i%cols;
								//								ci = i/cols;
								ci = i%cols;
								ri = i/cols;
								mItem.set(ri, ci, Double.parseDouble(vals[i]));
							}
							if (ci != cols - 1 | ri != rows - 1) {
								throw new WrongSizeConstant(oneconstant.getNodeName(), stepInfo.getMyDescription());
							}

							workspace.setVar(oneconstant.getNodeName(), mItem);

						}
					} // step constants loop
					stepInfo.setMyConstants(workspace);

					// Get script for this step
					NodeList scriptElemLst = step.getElementsByTagName("script");
					Element scriptElem = (Element) scriptElemLst.item(0);
					scriptElemLst = scriptElem.getChildNodes();				
					for (int ix=0; ix< scriptElemLst.getLength(); ix++) {
						Node scriptLine = scriptElemLst.item(ix);
						short ntype = scriptLine.getNodeType();
						if(ntype==Node.ELEMENT_NODE || ntype==Node.TEXT_NODE){
							//a child element to process
							String scriptLineText = scriptLine.getTextContent();
							if (scriptLineText.trim().length()>0) {
							  stepInfo.myScript.add(scriptLineText.trim());
							}
						}							
					}

					//Got everything for this step, store in step object
					mySteps.add(stepInfo);

				}
			} // steps loop
		} catch (Exception e) {
			e.printStackTrace();
		}
	}
	/*
	 * Get the size attribute from a node
	 * returns vector containing pair of integers representing the size vector
	 */
	private int[] getNodeSize(Element myNode) throws InputdataSizeInvalidException, InputdataSizeNotFoundException
	{
		int[] result = null;
		String attributestr = myNode.getAttribute("size");							  							   
		String szStr = attributestr.trim();
		szStr = szStr.replaceAll("[\\[\\]]","");					        
		String[] spltsz = szStr.split(",");

		if (spltsz.length == 2) {
			result = new int[2];
			result[0] = Integer.parseInt(spltsz[0]);
			result[1] = Integer.parseInt(spltsz[1]);
		} 
		return (result);
	}

	/*
	 * Test a matrix for being a scalar matrix (single element)
	 * returns boolean TRUE if m is a scalar
	 */
	private boolean isScalar(Matrix m) 
	{ 
		return (m.getColumnDimension() == 1 & m.getRowDimension() == 1); 
	}

	public String getModelType() {
		return myModelType;
	}

	public int getInputDataSize() {
		return myDataSize;
	}

	public Document getInformation() {
		return information;
	}

	/*
	 * Internal class used to hold the parts of one step
	 */
	class ScriptStep
	{
		private List<String> myScript = new ArrayList<String>();
		private String myDescription = "";
		private Workspace myConstants = new Workspace();

		public ScriptStep() { }

		public List<String> getMyScript() {
			return myScript;
		}

		public void setMyScript(List<String> myScript) {
			this.myScript = myScript;
		}

		public String getMyDescription() {
			return myDescription;
		}

		public void setMyDescription(String myDescription) {
			this.myDescription = myDescription;
		}

		public Workspace getMyConstants() {
			return myConstants;
		}

		public void setMyConstants(Workspace myConstants) {
			this.myConstants = myConstants;
		}
	}

	public Matrix getInputData() {
		return myData;
	}

	public void setInputData(Matrix inputData) {
		this.myData = inputData;
	}

	@SuppressWarnings("serial")
	public class InputdataInfoNotFoundException extends Exception {
		public InputdataInfoNotFoundException() {
			super("No inputdata tag found");
		}
	}
	@SuppressWarnings("serial")
	public class InputdataSizeInvalidException extends Exception {
		public InputdataSizeInvalidException() {
			super("Couldn't parse number of columns");
		}
	}
	@SuppressWarnings("serial")
	public class InputdataSizeNotFoundException extends Exception {
		public InputdataSizeNotFoundException() {
			super("Couldn't find inputdata \"size\" element");
		}
	}
	@SuppressWarnings("serial")
	public class InformationNotFoundException extends Exception {
		public InformationNotFoundException() {
			super("Couldn't find information element");
		}
	}
	@SuppressWarnings("serial")
	public class NoStepsFoundException extends Exception {
		public NoStepsFoundException() {
			super("Couldn't find any inputdata \"step\" elements");
		}
	}
	@SuppressWarnings("serial")
	public class UnparsableConstant extends Exception {
		public UnparsableConstant(String name, String stepDescription) {
			super("Unable to parse size or content for constant \"" + name + "\" in step \"" + stepDescription + "\"");
		}
	}
	@SuppressWarnings("serial")
	public class WrongSizeConstant extends Exception {
		public WrongSizeConstant(String name, String stepDescription) {
			super("Value parses to incorrect size for constant \"" + name + "\" in step \"" + stepDescription + "\"");
		}
	}
	@SuppressWarnings("serial")
	public class InputDataMissing extends Exception {
		public InputDataMissing() {
			super("Inputdata has not been assigned prior to calling apply");
		}
	}
	@SuppressWarnings("serial")
	public class BadScriptCommand extends Exception {
		public BadScriptCommand(String oneline) {
			super("Poorly formatted script command \"" + oneline + "\"");
		}
	}
	@SuppressWarnings("serial")
	public class MatrixDimensionException extends Exception {
		public MatrixDimensionException() {
			super("Dimension of the two matrices not suitable for this operation!");
		}
	}
	@SuppressWarnings("serial")
	public class UnknownFunctionException extends Exception {
		public UnknownFunctionException(String functionName) {
			super("Unrecognized function name: " + functionName);
		}
	}
	@SuppressWarnings("serial")
	public class ModelNotApplied extends Exception {
		public ModelNotApplied() {
			super("Apply method must be called before attempting to retrieve results");
		}
	}

	/*
	 * Test driver function
	 */
	public static void main(String[] args) {
//		String dirname = "C:\\Code\\evri\\model_exporter\\trunk\\interpreters\\MEInterpreter\\Java\\testfiles\\";
		String dirname = "C:\\Code\\evri\\model_exporter\\technical notes\\interpretertests\\javainterpreter\\";
//		String fname = dirname + "testing.xml";
//		String fname = dirname + "pca\\pcademomodel.xml";
//		String fname = dirname + "pls\\plsdemomodel.xml";
//		String fname = dirname + "plsda\\plsdademomodel.xml";
//		String fname = dirname + "pcr\\pcrdemomodel.xml";
//		String fname = dirname + "cls\\clsdemomodel.xml";
//		String fname = dirname + "svm\\svmdemomodel.xml";
//		String fname = dirname + "svmda\\svmdademomodel.xml";
		String fname = dirname + "ann\\anndemomodel.xml";
		
		try {			
			long startTime = System.currentTimeMillis();

			//create instance of a ModelInterpreter object using the specified file
			//could also pass an XmlDocument here (if the XML was stored in a database
			//instead of a file, for example)

			ModelInterpreter test = new ModelInterpreter(fname);

			//display some of the common model information
			String modeltype = test.getModelType();
			System.out.println("Model type: " + modeltype);
			System.out.println("Expected Data Size:" + test.getInputDataSize()); //getInputDataSize());

			//create data to pass to the model
			Matrix inmatrix = new Matrix(1, test.getInputDataSize());
			for (int i = 0; i < inmatrix.getColumnDimension(); i++) { 
				inmatrix.set(0, i, (i+1));
			};

			long startTimeApply = System.currentTimeMillis();            //start timer

			//set data and apply model            
			test.setInputData(inmatrix);
			test.apply();  //apply model

			//how long did it take to apply the model?
			System.out.println("apply() Elapsed Time (ms): " + (System.currentTimeMillis() - startTimeApply));

			//Typical outputs for a PLS model:
			Workspace res = test.getResults();
			if(modeltype.compareToIgnoreCase("PCA")!=0 & modeltype.compareToIgnoreCase("SVMDA")!=0) {
				Matrix yhat = test.getResults().getVar("yhat");
				System.out.println("yhat = " + yhat.get(0, 0));
			}

			if(modeltype.compareToIgnoreCase("SVM")!=0 & modeltype.compareToIgnoreCase("SVMDA")!=0 & modeltype.compareToIgnoreCase("ANN")!=0) {
				Matrix t2 = test.getResults().getVar("T2");
				Matrix q  = test.getResults().getVar("Q");
				System.out.println("T2 = " + test.getResults().getVar("T2").get(0, 0));
				System.out.println("Q = " + test.getResults().getVar("Q").get(0, 0));
			}

			if(modeltype.compareToIgnoreCase("SVM")==0 | modeltype.compareToIgnoreCase("SVMDA")==0) {
				Matrix t2 = test.getResults().getVar("nsvs");
				System.out.println("nsvs = " + test.getResults().getVar("nsvs").get(0, 0));
			}

			//List ALL contents of workspace  
			for(String varname : test.getMyWorkspace().getVarList()) {
				System.out.println(varname + " = \n" + test.getMyWorkspace().getVar(varname));
			}

			System.out.println("ModelInterpreter: Ending main. Elapsed Time:" + (System.currentTimeMillis() - startTime)/1000);
		} catch (Exception e) {
			e.printStackTrace();
		}
	}	
}
