' Cria um atalho do app na Area de Trabalho apontando para o Iniciar.vbs
Dim sh, fso, scriptDir, target, desktop, lnk
Set sh  = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
target = scriptDir & "\Iniciar.vbs"
desktop = sh.SpecialFolders("Desktop")

Set lnk = sh.CreateShortcut(desktop & "\Transferencia 1-Clique.lnk")
lnk.TargetPath = target
lnk.WorkingDirectory = scriptDir
lnk.IconLocation = "shell32.dll, 146"   ' icone de pasta/transferencia
lnk.Description = "Transferencia 1-Clique"
lnk.Save

MsgBox "Atalho criado na Area de Trabalho: 'Transferencia 1-Clique'." & vbCrLf & _
       "Voce pode arrasta-lo para a barra de tarefas para fixar.", 64, "Pronto"
