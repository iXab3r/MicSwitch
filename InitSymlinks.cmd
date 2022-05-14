@echo off
cd Sources
mklink /J /D PoeShared "../Submodules/PoeEye/PoeEye/PoeShared"
mklink /J /D PoeShared.Wpf "../Submodules/PoeEye/PoeEye/PoeShared.Wpf"
mklink /J /D PoeShared.Native "../Submodules/PoeEye/PoeEye/PoeShared.Native"
mklink /J /D PoeShared.Tests "../Submodules/PoeEye/PoeEye/PoeShared.Tests"
mklink /J /D PoeShared.Squirrel "../Submodules/PoeEye/PoeEye/PoeShared.Squirrel"
mklink /J /D PoeShared.Squirrel.Shared "../Submodules/PoeEye/PoeEye/PoeShared.Squirrel.Shared"
mklink /J /D WindowsHook "../Submodules/PoeEye/PoeEye/WindowsHook"

mklink /J /D PropertyBinder "../Submodules/PropertyBinder/PropertyBinder"
mklink /J /D PropertyBinder.Tests "../Submodules/PropertyBinder/PropertyBinder.Tests"