using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using CoreLib.Diagnostics;
using SharedLib;

namespace Client.ViewModels;

[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class PersonalFileViewModel : ExecuteViewModelBase
{
	private string caption;

	private string expandedPath;

	[Import]
	private GizmoClient Client { get; set; }

	[IgnorePropertyModification]
	public string Caption
	{
		get
		{
			return caption;
		}
		set
		{
			SetProperty(ref caption, value, "Caption");
		}
	}

	[IgnorePropertyModification]
	public string ExpandedPath
	{
		get
		{
			return expandedPath;
		}
		set
		{
			SetProperty(ref expandedPath, value, "ExpandedPath");
		}
	}

	protected override bool OnCanExecuteCommand(object param)
	{
		return !string.IsNullOrWhiteSpace(ExpandedPath);
	}

	protected override async void OnExecuteCommand(object param)
	{
		_ = 2;
		try
		{
			if (string.IsNullOrWhiteSpace(ExpandedPath))
			{
				return;
			}
			if (!(await Task.Run(() => File.Exists(ExpandedPath)).ConfigureAwait(continueOnCapturedContext: false)))
			{
				await Task.Run(() => Directory.CreateDirectory(ExpandedPath));
			}
			await CoreProcess.StartAsync(new CoreProcessStartInfo
			{
				FileName = ExpandedPath,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Client.TraceWrite(ex, "OnExecuteCommand", "D:\\My Documents\\Visual Studio 2015\\Projects\\Gizmo\\Gizmo.Client\\Gizmo.Client\\ViewModels\\PersonalFileViewModel.cs", 73);
		}
	}
}
