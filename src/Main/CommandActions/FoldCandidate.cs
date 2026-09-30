namespace Main;

record FoldCandidate(
    string VariableName,
    string RawVariableValue,
    string NormalizedVariableValue,
    string RawNewValue,
    string NormalizedNewValue,
    bool IsPath);