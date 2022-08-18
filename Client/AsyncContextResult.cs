namespace Client;

public class AsyncContextResult
{
	public bool Success { get; private set; }

	public ExecutionContext Context { get; private set; }

	public AsyncContextResult(ExecutionContext cx, bool result)
	{
		Context = cx;
		Success = result;
	}
}
