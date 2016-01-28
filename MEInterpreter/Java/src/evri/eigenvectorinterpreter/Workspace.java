package evri.eigenvectorinterpreter;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import Jama.Matrix;

public class Workspace {
	
	// (List<String>) Returns the alphabetically sorted list of names for all variables currently set in the
	// Workspace as a List<String> type. These names can be used with the getVar method to retrieve the values.
	private Map<String, Matrix> varMap;
	
	public Workspace() {
		varMap = new HashMap<String, Matrix>();
	}
	
	private Map<String, Matrix> getVarMap() {
		return varMap;
	}

	// Sets the variable specified by name with the matrix value
	public void setVar(String name, Matrix value) {
		varMap.put(name,  value);
	}
	
	// Copies all variables in the toadd workspace into the workspace
	public void setVar(Workspace toadd) {
		varMap.putAll(toadd.getVarMap());
	}

	// Retrieves the specified variable name from the workspace
	public Matrix getVar(String name) {
		return varMap.get(name);
	}
	
	public List<String> getVarList() {
		Set<String> keys = this.getVarMap().keySet();
		return new ArrayList<String>(keys);		
	}

	// True if the given variable name is currently set in the workspace.
	public Boolean isSet(String name) {
		return varMap.containsKey(name);
	}
	
	// Clears all values from the workspace.
	public void clearAll() {
		varMap.clear();
	}
}
