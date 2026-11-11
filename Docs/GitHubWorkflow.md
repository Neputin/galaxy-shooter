**Saving this game to GitHub**

Repository: https://github.com/Neputin/galaxy-shooter

The repository is private. Your project folder is a local Git repository, `main` is the primary branch, and `origin` points to its GitHub copy.

**Your normal save-and-upload routine**

First save your scripts and scenes in Unity. Exit Play mode before saving changes you intend to keep; temporary Play-mode changes normally disappear when you stop playing.

Open PowerShell in the project folder:

```powershell
Set-Location 'D:\unityrepoTest\My project'
unity vcs status
git add .
git diff --cached --stat
git commit -m 'Describe what you changed'
git push
```

`unity vcs status` shows changed Unity files and can highlight missing asset/metadata pairs. `git add .` selects your current changes for the next snapshot, while respecting `.gitignore`. `git diff --cached --stat` lets you review what that snapshot will contain. `git commit` creates the snapshot locally. `git push` uploads it to GitHub.

Saving a file in Unity does not automatically commit it or upload it. A commit also stays local until you push it.

Use a specific commit message, such as `Increase boss attack recovery time` or `Add a new shield pickup sprite`. Small commits are easier to understand and restore than one giant commit after many unrelated changes.

**Getting changes from another computer**

Close the Unity Editor before updating the files underneath it, then run:

```powershell
unity vcs sync
```

The Unity command accounts for Git LFS and checks whether the Editor is holding the project. Reopen the project after syncing.

**Cloning on another computer**

Install Git, Git LFS, Unity Hub, and Unity 6000.6.6f1. Sign into GitHub with an account that can access this private repository, then run:

```powershell
git lfs install
git clone https://github.com/Neputin/galaxy-shooter.git
Set-Location galaxy-shooter
git lfs pull
```

Add the folder to Unity Hub and let Unity restore its packages and import the assets. It is normal for `Library` to be absent initially; Unity generates it.

**What the setup files do**

`.gitignore` excludes generated folders, IDE files, temporary captures, and local tool metadata. It does not delete those files from your computer.

`.gitattributes` configures Unity text files and sends binary assets such as PNGs and WAVs through Git LFS. LFS stores a small reference in ordinary Git and the actual binary in separate GitHub storage. Its upload hook runs automatically during `git push`.

Unity `.meta` files store asset identifiers and import settings. Commit an asset together with its `.meta` file so references in scenes and prefabs survive on other computers. Move and rename assets through Unity where possible.

UnityYAMLMerge is configured locally for Unity scene and prefab merges. On another machine, configure its installed Editor's merge tool with:

```powershell
unity vcs merge-setup
unity vcs doctor
```

**Check your latest snapshot**

```powershell
git status
git log -5 --oneline
```

A clean status means all tracked local changes are committed. An up-to-date branch means that commit has also been pushed to its configured GitHub branch.
