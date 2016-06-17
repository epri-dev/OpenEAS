::*******************************************************************************************************
::  UpdateDependencies.bat - Gbtc
::
::  Copyright � 2013, Grid Protection Alliance.  All Rights Reserved.
::
::  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
::  the NOTICE file distributed with this work for additional information regarding copyright ownership.
::  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
::  not use this file except in compliance with the License. You may obtain a copy of the License at:
::
::      http://www.opensource.org/licenses/eclipse-1.0.php
::
::  Unless agreed to in writing, the subject software distributed under the License is distributed on an
::  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
::  License for the specific language governing permissions and limitations.
::
::  Code Modification History:
::  -----------------------------------------------------------------------------------------------------
::  08/16/2015 - Stephen C. Wills
::       Generated original version of source code.
::
::*******************************************************************************************************

@ECHO OFF

SETLOCAL

SET pwd="%CD%"
SET gwd="%LOCALAPPDATA%\Temp\openEAS"
SET git="%PROGRAMFILES(X86)%\Git\cmd\git.exe"
SET remote="git@github.com:GridProtectionAlliance/openXDA.git"
SET branch="sdbx"
SET source1="\\GPAWEB\NightlyBuilds\GridSolutionsFramework\Beta\Libraries\*.*"
SET target1="Source\Dependencies\GSF"
SET source2="\\gpaweb\NightlyBuilds\openHistorian\Beta\Applications\openHistorian\*.*"
SET target2="Source\Dependencies\GSF"
SET source3="\\GPAWEB\NightlyBuilds\openXDA\Beta\Libraries\*.*"
SET target3="Source\Dependencies\openXDA"
SET sourcemasterbuild="\\GPAWEB\NightlyBuilds\GridSolutionsFramework\Beta\Build Scripts\MasterBuild.buildproj"
SET targetmasterbuild="Build\Scripts"

ECHO.
ECHO Entering working directory...
IF EXIST %gwd% RMDIR /S /Q %gwd%
MKDIR %gwd%
CD /D %gwd%

ECHO.
ECHO Getting latest version...
%git% clone %remote% .
%git% checkout %branch%

ECHO.
ECHO Updating dependencies...
XCOPY %source1% %target1% /E /U /Y
XCOPY %source2% %target2% /E /U /Y
XCOPY %source3% %target3% /E /U /Y
XCOPY %sourcemasterbuild% %targetmasterbuild% /Y

ECHO.
ECHO Committing updates to local repository...
%git% add .
%git% commit -m "Updated GSF dependencies."

ECHO.
ECHO Pushing changes to remote repository...
%git% push
CD /D %pwd%

ECHO.
ECHO Update complete