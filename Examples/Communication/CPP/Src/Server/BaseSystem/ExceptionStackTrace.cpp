﻿//==========================================================================
//
//  File:        ExceptionStackTrace.cpp
//  Description: C++异常捕捉时获得代码栈
//  Version:     2021.03.24.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//  StackWalker.h / StackWalker.cpp
//  Author:      Jochen Kalmbach
//  Url:         http://www.codeproject.com/Articles/11132/Walking-the-callstack
//               https://stackwalker.codeplex.com/SourceControl/latest
//               https://github.com/JochenKalmbach/StackWalker
//  License:     The BSD 2-Clause License, http://www.opensource.org/licenses/bsd-license.php
//
//  gdb_check
//  http://stackoverflow.com/questions/3596781/how-to-detect-if-the-current-process-is-being-run-by-gdb
//  note: only work on Linux, not work on BSD
//
//  __cxa_throw reference
//  https://gist.github.com/nkuln/2020860
//  http://www.gnu.org/software/libc/manual/html_node/Backtraces.html
//
//==========================================================================

#if defined(_MSC_VER)

#ifndef _CRT_SECURE_NO_WARNINGS
#   define _CRT_SECURE_NO_WARNINGS
#endif

#endif

#include "ExceptionStackTrace.h"

#if defined(_WIN32)

#pragma warning(push)
#pragma warning(disable: 4091)

/**********************************************************************
 *
 * StackWalker.h
 *
 *
 *
 * LICENSE (http://www.opensource.org/licenses/bsd-license.php)
 *
 *   Copyright (c) 2005-2009, Jochen Kalmbach
 *   All rights reserved.
 *
 *   Redistribution and use in source and binary forms, with or without modification,
 *   are permitted provided that the following conditions are met:
 *
 *   Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 *   Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 *   Neither the name of Jochen Kalmbach nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without
 *   specific prior written permission.
 *   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 *   AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
 *   THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 *   ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
 *   FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 *   (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 *   LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 *   ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 *   (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 *   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * **********************************************************************/
#pragma once

#include <windows.h>

namespace
{

#if _MSC_VER < 1400
#   error "Compiler too old, not supported."
#endif

class StackWalkerInternal;  // forward
class StackWalker
{
public:
    typedef enum StackWalkOptions
    {
        // No addition info will be retrived
        // (only the address is available)
        RetrieveNone = 0,

        // Try to get the symbol-name
        RetrieveSymbol = 1,

        // Try to get the line for this symbol
        RetrieveLine = 2,

        // Try to retrieve the module-infos
        RetrieveModuleInfo = 4,

        // Also retrieve the version for the DLL/EXE
        RetrieveFileVersion = 8,

        // Contains all the abouve
        RetrieveVerbose = 0xF,

        // Generate a "good" symbol-search-path
        SymBuildPath = 0x10,

        // Also use the public Microsoft-Symbol-Server
        SymUseSymSrv = 0x20,

        // Contains all the abouve "Sym"-options
        SymAll = 0x30,

        // Contains all options (default)
        OptionsAll = 0x3F
    } StackWalkOptions;

    StackWalker(
        int options = OptionsAll, // 'int' is by design, to combine the enum-flags
        LPCSTR szSymPath = NULL,
        DWORD dwProcessId = GetCurrentProcessId(),
        HANDLE hProcess = GetCurrentProcess()
    );
    StackWalker(DWORD dwProcessId, HANDLE hProcess);
    virtual ~StackWalker();

    typedef BOOL(__stdcall *PReadProcessMemoryRoutine)(
        HANDLE      hProcess,
        DWORD64     qwBaseAddress,
        PVOID       lpBuffer,
        DWORD       nSize,
        LPDWORD     lpNumberOfBytesRead,
        LPVOID      pUserData  // optional data, which was passed in "ShowCallstack"
        );

    BOOL LoadModules();

    BOOL ShowCallstack(
        HANDLE hThread = GetCurrentThread(),
        const CONTEXT *context = NULL,
        PReadProcessMemoryRoutine readMemoryFunction = NULL,
        LPVOID pUserData = NULL  // optional to identify some data in the 'readMemoryFunction'-callback
    );

protected:
    enum { STACKWALK_MAX_NAMELEN = 4096 }; // max name length for found symbols

protected:
    // Entry for each Callstack-Entry
    typedef struct CallstackEntry
    {
        DWORD64 offset;  // if 0, we have no valid entry
        CHAR name[STACKWALK_MAX_NAMELEN];
        CHAR undName[STACKWALK_MAX_NAMELEN];
        CHAR undFullName[STACKWALK_MAX_NAMELEN];
        DWORD64 offsetFromSmybol;
        DWORD offsetFromLine;
        DWORD lineNumber;
        CHAR lineFileName[STACKWALK_MAX_NAMELEN];
        DWORD symType;
        LPCSTR symTypeString;
        CHAR moduleName[STACKWALK_MAX_NAMELEN];
        DWORD64 baseOfImage;
        CHAR loadedImageName[STACKWALK_MAX_NAMELEN];
    } CallstackEntry;

    typedef enum CallstackEntryType { firstEntry, nextEntry, lastEntry };

    virtual void OnSymInit(LPCSTR szSearchPath, DWORD symOptions, LPCSTR szUserName);
    virtual void OnLoadModule(LPCSTR img, LPCSTR mod, DWORD64 baseAddr, DWORD size, DWORD result, LPCSTR symType, LPCSTR pdbName, ULONGLONG fileVersion);
    virtual void OnCallstackEntry(CallstackEntryType eType, CallstackEntry &entry);
    virtual void OnDbgHelpErr(LPCSTR szFuncName, DWORD gle, DWORD64 addr);
    virtual void OnOutput(LPCSTR szText);

    StackWalkerInternal *m_sw;
    HANDLE m_hProcess;
    DWORD m_dwProcessId;
    BOOL m_modulesLoaded;
    LPSTR m_szSymPath;

    int m_options;
    int m_MaxRecursionCount;

    static BOOL __stdcall myReadProcMem(HANDLE hProcess, DWORD64 qwBaseAddress, PVOID lpBuffer, DWORD nSize, LPDWORD lpNumberOfBytesRead);

    friend StackWalkerInternal;
};  // class StackWalker


// The "ugly" assembler-implementation is needed for systems before XP
// If you have a new PSDK and you only compile for XP and later, then you can use
// the "RtlCaptureContext"
// Currently there is no define which determines the PSDK-Version...
// So we just use the compiler-version (and assumes that the PSDK is
// the one which was installed by the VS-IDE)

// INFO: If you want, you can use the RtlCaptureContext if you only target XP and later...
//       But I currently use it in x64/IA64 environments...
//#if defined(_M_IX86) && (_WIN32_WINNT <= 0x0500) && (_MSC_VER < 1400)

#if defined(_M_IX86)
#ifdef CURRENT_THREAD_VIA_EXCEPTION
// TODO: The following is not a "good" implementation,
// because the callstack is only valid in the "__except" block...
#define GET_CURRENT_CONTEXT_STACKWALKER_CODEPLEX(c, contextFlags) \
  do { \
    memset(&c, 0, sizeof(CONTEXT)); \
    EXCEPTION_POINTERS *pExp = NULL; \
    __try { \
      throw 0; \
    } __except( ( (pExp = GetExceptionInformation()) ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_EXECUTE_HANDLER)) {} \
    if (pExp != NULL) \
      memcpy(&c, pExp->ContextRecord, sizeof(CONTEXT)); \
      c.ContextFlags = contextFlags; \
  } while(0);
#else
// The following should be enough for walking the callstack...
#define GET_CURRENT_CONTEXT_STACKWALKER_CODEPLEX(c, contextFlags) \
  do { \
    memset(&c, 0, sizeof(CONTEXT)); \
    c.ContextFlags = contextFlags; \
    __asm    call x \
    __asm x: pop eax \
    __asm    mov c.Eip, eax \
    __asm    mov c.Ebp, ebp \
    __asm    mov c.Esp, esp \
  } while(0);
#endif

#else

// The following is defined for x86 (XP and higher), x64 and IA64:
#define GET_CURRENT_CONTEXT_STACKWALKER_CODEPLEX(c, contextFlags) \
  do { \
    memset(&c, 0, sizeof(CONTEXT)); \
    c.ContextFlags = contextFlags; \
    RtlCaptureContext(&c); \
} while(0);
#endif

}

/**********************************************************************
 *
 * StackWalker.cpp
 * http://stackwalker.codeplex.com/
 *
 *
 * History:
 *  2005-07-27   v1    - First public release on http://www.codeproject.com/
 *                       http://www.codeproject.com/threads/StackWalker.asp
 *  2005-07-28   v2    - Changed the params of the constructor and ShowCallstack
 *                       (to simplify the usage)
 *  2005-08-01   v3    - Changed to use 'CONTEXT_FULL' instead of CONTEXT_ALL
 *                       (should also be enough)
 *                     - Changed to compile correctly with the PSDK of VC7.0
 *                       (GetFileVersionInfoSizeA and GetFileVersionInfoA is wrongly defined:
 *                        it uses LPSTR instead of LPCSTR as first paremeter)
 *                     - Added declarations to support VC5/6 without using 'dbghelp.h'
 *                     - Added a 'pUserData' member to the ShowCallstack function and the
 *                       PReadProcessMemoryRoutine declaration (to pass some user-defined data,
 *                       which can be used in the readMemoryFunction-callback)
 *  2005-08-02   v4    - OnSymInit now also outputs the OS-Version by default
 *                     - Added example for doing an exception-callstack-walking in main.cpp
 *                       (thanks to owillebo: http://www.codeproject.com/script/profile/whos_who.asp?id=536268)
 *  2005-08-05   v5    - Removed most Lint (http://www.gimpel.com/) errors... thanks to Okko Willeboordse!
 *  2008-08-04   v6    - Fixed Bug: Missing LEAK-end-tag
 *                       http://www.codeproject.com/KB/applications/leakfinder.aspx?msg=2502890#xx2502890xx
 *                       Fixed Bug: Compiled with "WIN32_LEAN_AND_MEAN"
 *                       http://www.codeproject.com/KB/applications/leakfinder.aspx?msg=1824718#xx1824718xx
 *                       Fixed Bug: Compiling with "/Wall"
 *                       http://www.codeproject.com/KB/threads/StackWalker.aspx?msg=2638243#xx2638243xx
 *                       Fixed Bug: Now checking SymUseSymSrv
 *                       http://www.codeproject.com/KB/threads/StackWalker.aspx?msg=1388979#xx1388979xx
 *                       Fixed Bug: Support for recursive function calls
 *                       http://www.codeproject.com/KB/threads/StackWalker.aspx?msg=1434538#xx1434538xx
 *                       Fixed Bug: Missing FreeLibrary call in "GetModuleListTH32"
 *                       http://www.codeproject.com/KB/threads/StackWalker.aspx?msg=1326923#xx1326923xx
 *                       Fixed Bug: SymDia is number 7, not 9!
 *  2008-09-11   v7      For some (undocumented) reason, dbhelp.h is needing a packing of 8!
 *                       Thanks to Teajay which reported the bug...
 *                       http://www.codeproject.com/KB/applications/leakfinder.aspx?msg=2718933#xx2718933xx
 *  2008-11-27   v8      Debugging Tools for Windows are now stored in a different directory
 *                       Thanks to Luiz Salamon which reported this "bug"...
 *                       http://www.codeproject.com/KB/threads/StackWalker.aspx?msg=2822736#xx2822736xx
 *  2009-04-10   v9      License slihtly corrected (<ORGANIZATION> replaced)
 *  2009-11-01   v10     Moved to http://stackwalker.codeplex.com/
 *  2009-11-02   v11     Now try to use IMAGEHLP_MODULE64_V3 if available
 *  2010-04-15   v12     Added support for VS2010 RTM
 *  2010-05-25   v13     Now using secure MyStrcCpy. Thanks to luke.simon:
 *                       http://www.codeproject.com/KB/applications/leakfinder.aspx?msg=3477467#xx3477467xx
 *  2013-01-07   v14     Runtime Check Error VS2010 Debug Builds fixed:
 *                       http://stackwalker.codeplex.com/workitem/10511
 *
 *
 * LICENSE (http://www.opensource.org/licenses/bsd-license.php)
 *
 *   Copyright (c) 2005-2013, Jochen Kalmbach
 *   All rights reserved.
 *
 *   Redistribution and use in source and binary forms, with or without modification,
 *   are permitted provided that the following conditions are met:
 *
 *   Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 *   Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 *   Neither the name of Jochen Kalmbach nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without
 *   specific prior written permission.
 *   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 *   AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
 *   THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 *   ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
 *   FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 *   (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 *   LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 *   ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 *   (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 *   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 **********************************************************************/
#include <windows.h>
#include <tchar.h>
#include <stdio.h>
#include <stdlib.h>
#pragma comment(lib, "version.lib")  // for "VerQueryValue"
#pragma warning(disable:4826)

//#include "StackWalker.h"


// If VC7 and later, then use the shipped 'dbghelp.h'-file
#include <dbghelp.h>

namespace
{

static void MyStrCpy(char* szDest, size_t nMaxDestSize, const char* szSrc)
{
  if (nMaxDestSize <= 0) return;
  strncpy_s(szDest, nMaxDestSize, szSrc, _TRUNCATE);
  szDest[nMaxDestSize-1] = 0;  // INFO: _TRUNCATE will ensure that it is nul-terminated; but with older compilers (<1400) it uses "strncpy" and this does not!)
}  // MyStrCpy

// Normally it should be enough to use 'CONTEXT_FULL' (better would be 'CONTEXT_ALL')
#define USED_CONTEXT_FLAGS CONTEXT_FULL


class StackWalkerInternal
{
public:
  StackWalkerInternal(StackWalker *parent, HANDLE hProcess)
  {
    m_parent = parent;
    m_hDbhHelp = NULL;
    pSC = NULL;
    m_hProcess = hProcess;
    m_szSymPath = NULL;
    pSFTA = NULL;
    pSGLFA = NULL;
    pSGMB = NULL;
    pSGMI = NULL;
    pSGO = NULL;
    pSGSFA = NULL;
    pSI = NULL;
    pSLM = NULL;
    pSSO = NULL;
    pSW = NULL;
    pUDSN = NULL;
    pSGSP = NULL;
  }
  ~StackWalkerInternal()
  {
    if (pSC != NULL)
      pSC(m_hProcess);  // SymCleanup
    if (m_hDbhHelp != NULL)
      FreeLibrary(m_hDbhHelp);
    m_hDbhHelp = NULL;
    m_parent = NULL;
    if(m_szSymPath != NULL)
      free(m_szSymPath);
    m_szSymPath = NULL;
  }
  BOOL Init(LPCSTR szSymPath)
  {
    if (m_parent == NULL)
      return FALSE;
    // Dynamically load the Entry-Points for dbghelp.dll:
    m_hDbhHelp = LoadLibrary( _T("dbghelp.dll") );
    if (m_hDbhHelp == NULL)
      return FALSE;
    pSI = (tSI) GetProcAddress(m_hDbhHelp, "SymInitialize" );
    pSC = (tSC) GetProcAddress(m_hDbhHelp, "SymCleanup" );

    pSW = (tSW) GetProcAddress(m_hDbhHelp, "StackWalk64" );
    pSGO = (tSGO) GetProcAddress(m_hDbhHelp, "SymGetOptions" );
    pSSO = (tSSO) GetProcAddress(m_hDbhHelp, "SymSetOptions" );

    pSFTA = (tSFTA) GetProcAddress(m_hDbhHelp, "SymFunctionTableAccess64" );
    pSGLFA = (tSGLFA) GetProcAddress(m_hDbhHelp, "SymGetLineFromAddr64" );
    pSGMB = (tSGMB) GetProcAddress(m_hDbhHelp, "SymGetModuleBase64" );
    pSGMI = (tSGMI) GetProcAddress(m_hDbhHelp, "SymGetModuleInfo64" );
    pSGSFA = (tSGSFA) GetProcAddress(m_hDbhHelp, "SymGetSymFromAddr64" );
    pUDSN = (tUDSN) GetProcAddress(m_hDbhHelp, "UnDecorateSymbolName" );
    pSLM = (tSLM) GetProcAddress(m_hDbhHelp, "SymLoadModule64" );
    pSGSP =(tSGSP) GetProcAddress(m_hDbhHelp, "SymGetSearchPath" );

    if ( pSC == NULL || pSFTA == NULL || pSGMB == NULL || pSGMI == NULL ||
      pSGO == NULL || pSGSFA == NULL || pSI == NULL || pSSO == NULL ||
      pSW == NULL || pUDSN == NULL || pSLM == NULL )
    {
      FreeLibrary(m_hDbhHelp);
      m_hDbhHelp = NULL;
      pSC = NULL;
      return FALSE;
    }

    // SymInitialize
    if (szSymPath != NULL)
      m_szSymPath = _strdup(szSymPath);
    if (this->pSI(m_hProcess, m_szSymPath, FALSE) == FALSE)
      this->m_parent->OnDbgHelpErr("SymInitialize", GetLastError(), 0);

    DWORD symOptions = this->pSGO();  // SymGetOptions
    symOptions |= SYMOPT_LOAD_LINES;
    symOptions |= SYMOPT_FAIL_CRITICAL_ERRORS;
    //symOptions |= SYMOPT_NO_PROMPTS;
    // SymSetOptions
    symOptions = this->pSSO(symOptions);

    char buf[StackWalker::STACKWALK_MAX_NAMELEN] = {0};
    if (this->pSGSP != NULL)
    {
      if (this->pSGSP(m_hProcess, buf, StackWalker::STACKWALK_MAX_NAMELEN) == FALSE)
        this->m_parent->OnDbgHelpErr("SymGetSearchPath", GetLastError(), 0);
    }

#if _M_ARM
    this->m_parent->OnSymInit(buf, symOptions, "Unknown");
#else
    char szUserName[1024] = {0};
    DWORD dwSize = 1024;
    GetUserNameA(szUserName, &dwSize);
    this->m_parent->OnSymInit(buf, symOptions, szUserName);
#endif

    return TRUE;
  }

  StackWalker *m_parent;

  HMODULE m_hDbhHelp;
  HANDLE m_hProcess;
  LPSTR m_szSymPath;

#pragma pack(push,8)
typedef struct IMAGEHLP_MODULE64_V3 {
    DWORD    SizeOfStruct;           // set to sizeof(IMAGEHLP_MODULE64)
    DWORD64  BaseOfImage;            // base load address of module
    DWORD    ImageSize;              // virtual size of the loaded module
    DWORD    TimeDateStamp;          // date/time stamp from pe header
    DWORD    CheckSum;               // checksum from the pe header
    DWORD    NumSyms;                // number of symbols in the symbol table
    SYM_TYPE SymType;                // type of symbols loaded
    CHAR     ModuleName[32];         // module name
    CHAR     ImageName[256];         // image name
    CHAR     LoadedImageName[256];   // symbol file name
    // new elements: 07-Jun-2002
    CHAR     LoadedPdbName[256];     // pdb file name
    DWORD    CVSig;                  // Signature of the CV record in the debug directories
    CHAR     CVData[MAX_PATH * 3];   // Contents of the CV record
    DWORD    PdbSig;                 // Signature of PDB
    GUID     PdbSig70;               // Signature of PDB (VC 7 and up)
    DWORD    PdbAge;                 // DBI age of pdb
    BOOL     PdbUnmatched;           // loaded an unmatched pdb
    BOOL     DbgUnmatched;           // loaded an unmatched dbg
    BOOL     LineNumbers;            // we have line number information
    BOOL     GlobalSymbols;          // we have internal symbol information
    BOOL     TypeInfo;               // we have type information
    // new elements: 17-Dec-2003
    BOOL     SourceIndexed;          // pdb supports source server
    BOOL     Publics;                // contains public symbols
};

typedef struct IMAGEHLP_MODULE64_V2 {
    DWORD    SizeOfStruct;           // set to sizeof(IMAGEHLP_MODULE64)
    DWORD64  BaseOfImage;            // base load address of module
    DWORD    ImageSize;              // virtual size of the loaded module
    DWORD    TimeDateStamp;          // date/time stamp from pe header
    DWORD    CheckSum;               // checksum from the pe header
    DWORD    NumSyms;                // number of symbols in the symbol table
    SYM_TYPE SymType;                // type of symbols loaded
    CHAR     ModuleName[32];         // module name
    CHAR     ImageName[256];         // image name
    CHAR     LoadedImageName[256];   // symbol file name
};
#pragma pack(pop)


  // SymCleanup()
  typedef BOOL (__stdcall *tSC)( IN HANDLE hProcess );
  tSC pSC;

  // SymFunctionTableAccess64()
  typedef PVOID (__stdcall *tSFTA)( HANDLE hProcess, DWORD64 AddrBase );
  tSFTA pSFTA;

  // SymGetLineFromAddr64()
  typedef BOOL (__stdcall *tSGLFA)( IN HANDLE hProcess, IN DWORD64 dwAddr,
    OUT PDWORD pdwDisplacement, OUT PIMAGEHLP_LINE64 Line );
  tSGLFA pSGLFA;

  // SymGetModuleBase64()
  typedef DWORD64 (__stdcall *tSGMB)( IN HANDLE hProcess, IN DWORD64 dwAddr );
  tSGMB pSGMB;

  // SymGetModuleInfo64()
  typedef BOOL (__stdcall *tSGMI)( IN HANDLE hProcess, IN DWORD64 dwAddr, OUT IMAGEHLP_MODULE64_V3 *ModuleInfo );
  tSGMI pSGMI;

  // SymGetOptions()
  typedef DWORD (__stdcall *tSGO)( VOID );
  tSGO pSGO;

  // SymGetSymFromAddr64()
  typedef BOOL (__stdcall *tSGSFA)( IN HANDLE hProcess, IN DWORD64 dwAddr,
    OUT PDWORD64 pdwDisplacement, OUT PIMAGEHLP_SYMBOL64 Symbol );
  tSGSFA pSGSFA;

  // SymInitialize()
  typedef BOOL (__stdcall *tSI)( IN HANDLE hProcess, IN PSTR UserSearchPath, IN BOOL fInvadeProcess );
  tSI pSI;

  // SymLoadModule64()
  typedef DWORD64 (__stdcall *tSLM)( IN HANDLE hProcess, IN HANDLE hFile,
    IN PSTR ImageName, IN PSTR ModuleName, IN DWORD64 BaseOfDll, IN DWORD SizeOfDll );
  tSLM pSLM;

  // SymSetOptions()
  typedef DWORD (__stdcall *tSSO)( IN DWORD SymOptions );
  tSSO pSSO;

  // StackWalk64()
  typedef BOOL (__stdcall *tSW)(
    DWORD MachineType,
    HANDLE hProcess,
    HANDLE hThread,
    LPSTACKFRAME64 StackFrame,
    PVOID ContextRecord,
    PREAD_PROCESS_MEMORY_ROUTINE64 ReadMemoryRoutine,
    PFUNCTION_TABLE_ACCESS_ROUTINE64 FunctionTableAccessRoutine,
    PGET_MODULE_BASE_ROUTINE64 GetModuleBaseRoutine,
    PTRANSLATE_ADDRESS_ROUTINE64 TranslateAddress );
  tSW pSW;

  // UnDecorateSymbolName()
  typedef DWORD (__stdcall WINAPI *tUDSN)( PCSTR DecoratedName, PSTR UnDecoratedName,
    DWORD UndecoratedLength, DWORD Flags );
  tUDSN pUDSN;

  typedef BOOL (__stdcall WINAPI *tSGSP)(HANDLE hProcess, PSTR SearchPath, DWORD SearchPathLength);
  tSGSP pSGSP;


private:
  // **************************************** ToolHelp32 ************************
  #define MAX_MODULE_NAME32 255
  #define TH32CS_SNAPMODULE   0x00000008
  #pragma pack( push, 8 )
  typedef struct tagMODULEENTRY32
  {
      DWORD   dwSize;
      DWORD   th32ModuleID;       // This module
      DWORD   th32ProcessID;      // owning process
      DWORD   GlblcntUsage;       // Global usage count on the module
      DWORD   ProccntUsage;       // Module usage count in th32ProcessID's context
      BYTE  * modBaseAddr;        // Base address of module in th32ProcessID's context
      DWORD   modBaseSize;        // Size in bytes of module starting at modBaseAddr
      HMODULE hModule;            // The hModule of this module in th32ProcessID's context
      char    szModule[MAX_MODULE_NAME32 + 1];
      char    szExePath[MAX_PATH];
  } MODULEENTRY32;
  typedef MODULEENTRY32 *  PMODULEENTRY32;
  typedef MODULEENTRY32 *  LPMODULEENTRY32;
  #pragma pack( pop )

  BOOL GetModuleListTH32(HANDLE hProcess, DWORD pid)
  {
    // CreateToolhelp32Snapshot()
    typedef HANDLE (__stdcall *tCT32S)(DWORD dwFlags, DWORD th32ProcessID);
    // Module32First()
    typedef BOOL (__stdcall *tM32F)(HANDLE hSnapshot, LPMODULEENTRY32 lpme);
    // Module32Next()
    typedef BOOL (__stdcall *tM32N)(HANDLE hSnapshot, LPMODULEENTRY32 lpme);

    // try both dlls...
    const TCHAR *dllname[] = { _T("kernel32.dll"), _T("tlhelp32.dll") };
    HINSTANCE hToolhelp = NULL;
    tCT32S pCT32S = NULL;
    tM32F pM32F = NULL;
    tM32N pM32N = NULL;

    HANDLE hSnap;
    MODULEENTRY32 me;
    me.dwSize = sizeof(me);
    BOOL keepGoing;
    size_t i;

    for (i = 0; i<(sizeof(dllname) / sizeof(dllname[0])); i++ )
    {
      hToolhelp = LoadLibrary( dllname[i] );
      if (hToolhelp == NULL)
        continue;
      pCT32S = (tCT32S) GetProcAddress(hToolhelp, "CreateToolhelp32Snapshot");
      pM32F = (tM32F) GetProcAddress(hToolhelp, "Module32First");
      pM32N = (tM32N) GetProcAddress(hToolhelp, "Module32Next");
      if ( (pCT32S != NULL) && (pM32F != NULL) && (pM32N != NULL) )
        break; // found the functions!
      FreeLibrary(hToolhelp);
      hToolhelp = NULL;
    }

    if (hToolhelp == NULL)
      return FALSE;

    hSnap = pCT32S( TH32CS_SNAPMODULE, pid );
    if (hSnap == (HANDLE) -1)
    {
      FreeLibrary(hToolhelp);
      return FALSE;
    }

    keepGoing = !!pM32F( hSnap, &me );
    int cnt = 0;
    while (keepGoing)
    {
      this->LoadModule(hProcess, me.szExePath, me.szModule, (DWORD64) me.modBaseAddr, me.modBaseSize);
      cnt++;
      keepGoing = !!pM32N( hSnap, &me );
    }
    CloseHandle(hSnap);
    FreeLibrary(hToolhelp);
    if (cnt <= 0)
      return FALSE;
    return TRUE;
  }  // GetModuleListTH32

  // **************************************** PSAPI ************************
  typedef struct _MODULEINFO {
      LPVOID lpBaseOfDll;
      DWORD SizeOfImage;
      LPVOID EntryPoint;
  } MODULEINFO, *LPMODULEINFO;

  BOOL GetModuleListPSAPI(HANDLE hProcess)
  {
    // EnumProcessModules()
    typedef BOOL (__stdcall *tEPM)(HANDLE hProcess, HMODULE *lphModule, DWORD cb, LPDWORD lpcbNeeded );
    // GetModuleFileNameEx()
    typedef DWORD (__stdcall *tGMFNE)(HANDLE hProcess, HMODULE hModule, LPSTR lpFilename, DWORD nSize );
    // GetModuleBaseName()
    typedef DWORD (__stdcall *tGMBN)(HANDLE hProcess, HMODULE hModule, LPSTR lpFilename, DWORD nSize );
    // GetModuleInformation()
    typedef BOOL (__stdcall *tGMI)(HANDLE hProcess, HMODULE hModule, LPMODULEINFO pmi, DWORD nSize );

    HINSTANCE hPsapi;
    tEPM pEPM;
    tGMFNE pGMFNE;
    tGMBN pGMBN;
    tGMI pGMI;

    DWORD i;
    //ModuleEntry e;
    DWORD cbNeeded;
    MODULEINFO mi;
    HMODULE *hMods = 0;
    char *tt = NULL;
    char *tt2 = NULL;
    const SIZE_T TTBUFLEN = 8096;
    int cnt = 0;

    hPsapi = LoadLibrary( _T("psapi.dll") );
    if (hPsapi == NULL)
      return FALSE;

    pEPM = (tEPM) GetProcAddress( hPsapi, "EnumProcessModules" );
    pGMFNE = (tGMFNE) GetProcAddress( hPsapi, "GetModuleFileNameExA" );
    pGMBN = (tGMFNE) GetProcAddress( hPsapi, "GetModuleBaseNameA" );
    pGMI = (tGMI) GetProcAddress( hPsapi, "GetModuleInformation" );
    if ( (pEPM == NULL) || (pGMFNE == NULL) || (pGMBN == NULL) || (pGMI == NULL) )
    {
      // we couldn´t find all functions
      FreeLibrary(hPsapi);
      return FALSE;
    }

    hMods = (HMODULE*) malloc(sizeof(HMODULE) * (TTBUFLEN / sizeof(HMODULE)));
    tt = (char*) malloc(sizeof(char) * TTBUFLEN);
    tt2 = (char*) malloc(sizeof(char) * TTBUFLEN);
    if ( (hMods == NULL) || (tt == NULL) || (tt2 == NULL) )
      goto cleanup;

    if ( ! pEPM( hProcess, hMods, TTBUFLEN, &cbNeeded ) )
    {
      //_ftprintf(fLogFile, _T("%lu: EPM failed, GetLastError = %lu\n"), g_dwShowCount, gle );
      goto cleanup;
    }

    if ( cbNeeded > TTBUFLEN )
    {
      //_ftprintf(fLogFile, _T("%lu: More than %lu module handles. Huh?\n"), g_dwShowCount, lenof( hMods ) );
      goto cleanup;
    }

    for ( i = 0; i < cbNeeded / sizeof(hMods[0]); i++ )
    {
      // base address, size
      pGMI(hProcess, hMods[i], &mi, sizeof(mi));
      // image file name
      tt[0] = 0;
      pGMFNE(hProcess, hMods[i], tt, TTBUFLEN );
      // module name
      tt2[0] = 0;
      pGMBN(hProcess, hMods[i], tt2, TTBUFLEN );

      DWORD dwRes = this->LoadModule(hProcess, tt, tt2, (DWORD64) mi.lpBaseOfDll, mi.SizeOfImage);
      if (dwRes != ERROR_SUCCESS)
        this->m_parent->OnDbgHelpErr("LoadModule", dwRes, 0);
      cnt++;
    }

  cleanup:
    if (hPsapi != NULL) FreeLibrary(hPsapi);
    if (tt2 != NULL) free(tt2);
    if (tt != NULL) free(tt);
    if (hMods != NULL) free(hMods);

    return cnt != 0;
  }  // GetModuleListPSAPI

  DWORD LoadModule(HANDLE hProcess, LPCSTR img, LPCSTR mod, DWORD64 baseAddr, DWORD size)
  {
    CHAR *szImg = _strdup(img);
    CHAR *szMod = _strdup(mod);
    DWORD result = ERROR_SUCCESS;
    if ( (szImg == NULL) || (szMod == NULL) )
      result = ERROR_NOT_ENOUGH_MEMORY;
    else
    {
      if (pSLM(hProcess, 0, szImg, szMod, baseAddr, size) == 0)
        result = GetLastError();
    }
    ULONGLONG fileVersion = 0;
    if ( (m_parent != NULL) && (szImg != NULL) )
    {
      // try to retrive the file-version:
      if ( (this->m_parent->m_options & StackWalker::RetrieveFileVersion) != 0)
      {
        VS_FIXEDFILEINFO *fInfo = NULL;
        DWORD dwHandle;
        DWORD dwSize = GetFileVersionInfoSizeA(szImg, &dwHandle);
        if (dwSize > 0)
        {
          LPVOID vData = malloc(dwSize);
          if (vData != NULL)
          {
            if (GetFileVersionInfoA(szImg, dwHandle, dwSize, vData) != 0)
            {
              UINT len;
              TCHAR szSubBlock[] = _T("\\");
              if (VerQueryValue(vData, szSubBlock, (LPVOID*) &fInfo, &len) == 0)
                fInfo = NULL;
              else
              {
                fileVersion = ((ULONGLONG)fInfo->dwFileVersionLS) + ((ULONGLONG)fInfo->dwFileVersionMS << 32);
              }
            }
            free(vData);
          }
        }
      }

      // Retrive some additional-infos about the module
      IMAGEHLP_MODULE64_V3 Module;
      const char *szSymType = "-unknown-";
      if (this->GetModuleInfo(hProcess, baseAddr, &Module) != FALSE)
      {
        switch(Module.SymType)
        {
          case SymNone:
            szSymType = "-nosymbols-";
            break;
          case SymCoff:  // 1
            szSymType = "COFF";
            break;
          case SymCv:  // 2
            szSymType = "CV";
            break;
          case SymPdb:  // 3
            szSymType = "PDB";
            break;
          case SymExport:  // 4
            szSymType = "-exported-";
            break;
          case SymDeferred:  // 5
            szSymType = "-deferred-";
            break;
          case SymSym:  // 6
            szSymType = "SYM";
            break;
          case 7: // SymDia:
            szSymType = "DIA";
            break;
          case 8: //SymVirtual:
            szSymType = "Virtual";
            break;
        }
      }
      LPCSTR pdbName = Module.LoadedImageName;
      if (Module.LoadedPdbName[0] != 0)
        pdbName = Module.LoadedPdbName;
      this->m_parent->OnLoadModule(img, mod, baseAddr, size, result, szSymType, pdbName, fileVersion);
    }
    if (szImg != NULL) free(szImg);
    if (szMod != NULL) free(szMod);
    return result;
  }
public:
  BOOL LoadModules(HANDLE hProcess, DWORD dwProcessId)
  {
    // first try toolhelp32
    if (GetModuleListTH32(hProcess, dwProcessId))
      return true;
    // then try psapi
    return GetModuleListPSAPI(hProcess);
  }


  BOOL GetModuleInfo(HANDLE hProcess, DWORD64 baseAddr, IMAGEHLP_MODULE64_V3 *pModuleInfo)
  {
    memset(pModuleInfo, 0, sizeof(IMAGEHLP_MODULE64_V3));
    if(this->pSGMI == NULL)
    {
      SetLastError(ERROR_DLL_INIT_FAILED);
      return FALSE;
    }
    // First try to use the larger ModuleInfo-Structure
    pModuleInfo->SizeOfStruct = sizeof(IMAGEHLP_MODULE64_V3);
    void *pData = malloc(4096); // reserve enough memory, so the bug in v6.3.5.1 does not lead to memory-overwrites...
    if (pData == NULL)
    {
      SetLastError(ERROR_NOT_ENOUGH_MEMORY);
      return FALSE;
    }
    memcpy(pData, pModuleInfo, sizeof(IMAGEHLP_MODULE64_V3));
    static bool s_useV3Version = true;
    if (s_useV3Version)
    {
      if (this->pSGMI(hProcess, baseAddr, (IMAGEHLP_MODULE64_V3*) pData) != FALSE)
      {
        // only copy as much memory as is reserved...
        memcpy(pModuleInfo, pData, sizeof(IMAGEHLP_MODULE64_V3));
        pModuleInfo->SizeOfStruct = sizeof(IMAGEHLP_MODULE64_V3);
        free(pData);
        return TRUE;
      }
      s_useV3Version = false;  // to prevent unneccessarry calls with the larger struct...
    }

    // could not retrive the bigger structure, try with the smaller one (as defined in VC7.1)...
    pModuleInfo->SizeOfStruct = sizeof(IMAGEHLP_MODULE64_V2);
    memcpy(pData, pModuleInfo, sizeof(IMAGEHLP_MODULE64_V2));
    if (this->pSGMI(hProcess, baseAddr, (IMAGEHLP_MODULE64_V3*) pData) != FALSE)
    {
      // only copy as much memory as is reserved...
      memcpy(pModuleInfo, pData, sizeof(IMAGEHLP_MODULE64_V2));
      pModuleInfo->SizeOfStruct = sizeof(IMAGEHLP_MODULE64_V2);
      free(pData);
      return TRUE;
    }
    free(pData);
    SetLastError(ERROR_DLL_INIT_FAILED);
    return FALSE;
  }
};

// #############################################################
StackWalker::StackWalker(DWORD dwProcessId, HANDLE hProcess)
{
  this->m_options = OptionsAll;
  this->m_modulesLoaded = FALSE;
  this->m_hProcess = hProcess;
  this->m_sw = new StackWalkerInternal(this, this->m_hProcess);
  this->m_dwProcessId = dwProcessId;
  this->m_szSymPath = NULL;
  this->m_MaxRecursionCount = 1000;
}
StackWalker::StackWalker(int options, LPCSTR szSymPath, DWORD dwProcessId, HANDLE hProcess)
{
  this->m_options = options;
  this->m_modulesLoaded = FALSE;
  this->m_hProcess = hProcess;
  this->m_sw = new StackWalkerInternal(this, this->m_hProcess);
  this->m_dwProcessId = dwProcessId;
  if (szSymPath != NULL)
  {
    this->m_szSymPath = _strdup(szSymPath);
    this->m_options |= SymBuildPath;
  }
  else
    this->m_szSymPath = NULL;
  this->m_MaxRecursionCount = 1000;
}

StackWalker::~StackWalker()
{
  if (m_szSymPath != NULL)
    free(m_szSymPath);
  m_szSymPath = NULL;
  if (this->m_sw != NULL)
    delete this->m_sw;
  this->m_sw = NULL;
}

BOOL StackWalker::LoadModules()
{
  if (this->m_sw == NULL)
  {
    SetLastError(ERROR_DLL_INIT_FAILED);
    return FALSE;
  }
  if (m_modulesLoaded != FALSE)
    return TRUE;

  // Build the sym-path:
  char *szSymPath = NULL;
  if ( (this->m_options & SymBuildPath) != 0)
  {
    const size_t nSymPathLen = 4096;
    szSymPath = (char*) malloc(nSymPathLen);
    if (szSymPath == NULL)
    {
      SetLastError(ERROR_NOT_ENOUGH_MEMORY);
      return FALSE;
    }
    szSymPath[0] = 0;
    // Now first add the (optional) provided sympath:
    if (this->m_szSymPath != NULL)
    {
      strcat_s(szSymPath, nSymPathLen, this->m_szSymPath);
      strcat_s(szSymPath, nSymPathLen, ";");
    }

    strcat_s(szSymPath, nSymPathLen, ".;");

    const size_t nTempLen = 1024;
    char szTemp[nTempLen];
    // Now add the current directory:
    if (GetCurrentDirectoryA(nTempLen, szTemp) > 0)
    {
      szTemp[nTempLen-1] = 0;
      strcat_s(szSymPath, nSymPathLen, szTemp);
      strcat_s(szSymPath, nSymPathLen, ";");
    }

    // Now add the path for the main-module:
    if (GetModuleFileNameA(NULL, szTemp, nTempLen) > 0)
    {
      szTemp[nTempLen-1] = 0;
      for (char *p = (szTemp+strlen(szTemp)-1); p >= szTemp; --p)
      {
        // locate the rightmost path separator
        if ( (*p == '\\') || (*p == '/') || (*p == ':') )
        {
          *p = 0;
          break;
        }
      }  // for (search for path separator...)
      if (strlen(szTemp) > 0)
      {
        strcat_s(szSymPath, nSymPathLen, szTemp);
        strcat_s(szSymPath, nSymPathLen, ";");
      }
    }
    if (GetEnvironmentVariableA("_NT_SYMBOL_PATH", szTemp, nTempLen) > 0)
    {
      szTemp[nTempLen-1] = 0;
      strcat_s(szSymPath, nSymPathLen, szTemp);
      strcat_s(szSymPath, nSymPathLen, ";");
    }
    if (GetEnvironmentVariableA("_NT_ALTERNATE_SYMBOL_PATH", szTemp, nTempLen) > 0)
    {
      szTemp[nTempLen-1] = 0;
      strcat_s(szSymPath, nSymPathLen, szTemp);
      strcat_s(szSymPath, nSymPathLen, ";");
    }
    if (GetEnvironmentVariableA("SYSTEMROOT", szTemp, nTempLen) > 0)
    {
      szTemp[nTempLen-1] = 0;
      strcat_s(szSymPath, nSymPathLen, szTemp);
      strcat_s(szSymPath, nSymPathLen, ";");
      // also add the "system32"-directory:
      strcat_s(szTemp, nTempLen, "\\system32");
      strcat_s(szSymPath, nSymPathLen, szTemp);
      strcat_s(szSymPath, nSymPathLen, ";");
    }

    if ( (this->m_options & SymUseSymSrv) != 0)
    {
      if (GetEnvironmentVariableA("SYSTEMDRIVE", szTemp, nTempLen) > 0)
      {
        szTemp[nTempLen-1] = 0;
        strcat_s(szSymPath, nSymPathLen, "SRV*");
        strcat_s(szSymPath, nSymPathLen, szTemp);
        strcat_s(szSymPath, nSymPathLen, "\\websymbols");
        strcat_s(szSymPath, nSymPathLen, "*http://msdl.microsoft.com/download/symbols;");
      }
      else
        strcat_s(szSymPath, nSymPathLen, "SRV*c:\\websymbols*http://msdl.microsoft.com/download/symbols;");
    }
  }  // if SymBuildPath

  // First Init the whole stuff...
  BOOL bRet = this->m_sw->Init(szSymPath);
  if (szSymPath != NULL) free(szSymPath); szSymPath = NULL;
  if (bRet == FALSE)
  {
    this->OnDbgHelpErr("Error while initializing dbghelp.dll", 0, 0);
    SetLastError(ERROR_DLL_INIT_FAILED);
    return FALSE;
  }

  bRet = this->m_sw->LoadModules(this->m_hProcess, this->m_dwProcessId);
  if (bRet != FALSE)
    m_modulesLoaded = TRUE;
  return bRet;
}


// The following is used to pass the "userData"-Pointer to the user-provided readMemoryFunction
// This has to be done due to a problem with the "hProcess"-parameter in x64...
// Because this class is in no case multi-threading-enabled (because of the limitations
// of dbghelp.dll) it is "safe" to use a static-variable
static StackWalker::PReadProcessMemoryRoutine s_readMemoryFunction = NULL;
static LPVOID s_readMemoryFunction_UserData = NULL;

BOOL StackWalker::ShowCallstack(HANDLE hThread, const CONTEXT *context, PReadProcessMemoryRoutine readMemoryFunction, LPVOID pUserData)
{
  CONTEXT c;
  CallstackEntry csEntry;
  IMAGEHLP_SYMBOL64 *pSym = NULL;
  StackWalkerInternal::IMAGEHLP_MODULE64_V3 Module;
  IMAGEHLP_LINE64 Line;
  int frameNum;
  bool bLastEntryCalled = true;
  int curRecursionCount = 0;

  if (m_modulesLoaded == FALSE)
    this->LoadModules();  // ignore the result...

  if (this->m_sw->m_hDbhHelp == NULL)
  {
    SetLastError(ERROR_DLL_INIT_FAILED);
    return FALSE;
  }

  s_readMemoryFunction = readMemoryFunction;
  s_readMemoryFunction_UserData = pUserData;

  if (context == NULL)
  {
    // If no context is provided, capture the context
    // See: https://stackwalker.codeplex.com/discussions/446958
#if _WIN32_WINNT <= 0x0501
      // If we need to support XP, we need to use the "old way", because "GetThreadId" is not available!
    if (hThread == GetCurrentThread())
#else
    if (GetThreadId(hThread) == GetCurrentThreadId())
#endif
    {
      GET_CURRENT_CONTEXT_STACKWALKER_CODEPLEX(c, USED_CONTEXT_FLAGS);
    }
    else
    {
      SuspendThread(hThread);
      memset(&c, 0, sizeof(CONTEXT));
      c.ContextFlags = USED_CONTEXT_FLAGS;

      // TODO: Detect if you want to get a thread context of a different process, which is running a different processor architecture...
      // This does only work if we are x64 and the target process is x64 or x86;
      // It cannnot work, if this process is x64 and the target process is x64... this is not supported...
      // See also: http://www.howzatt.demon.co.uk/articles/DebuggingInWin64.html
      if (GetThreadContext(hThread, &c) == FALSE)
      {
        ResumeThread(hThread);
        return FALSE;
      }
    }
  }
  else
    c = *context;

  // init STACKFRAME for first call
  STACKFRAME64 s; // in/out stackframe
  memset(&s, 0, sizeof(s));
  DWORD imageType;
#ifdef _M_IX86
  // normally, call ImageNtHeader() and use machine info from PE header
  imageType = IMAGE_FILE_MACHINE_I386;
  s.AddrPC.Offset = c.Eip;
  s.AddrPC.Mode = AddrModeFlat;
  s.AddrFrame.Offset = c.Ebp;
  s.AddrFrame.Mode = AddrModeFlat;
  s.AddrStack.Offset = c.Esp;
  s.AddrStack.Mode = AddrModeFlat;
#elif _M_X64
  imageType = IMAGE_FILE_MACHINE_AMD64;
  s.AddrPC.Offset = c.Rip;
  s.AddrPC.Mode = AddrModeFlat;
  s.AddrFrame.Offset = c.Rsp;
  s.AddrFrame.Mode = AddrModeFlat;
  s.AddrStack.Offset = c.Rsp;
  s.AddrStack.Mode = AddrModeFlat;
#elif _M_IA64
  imageType = IMAGE_FILE_MACHINE_IA64;
  s.AddrPC.Offset = c.StIIP;
  s.AddrPC.Mode = AddrModeFlat;
  s.AddrFrame.Offset = c.IntSp;
  s.AddrFrame.Mode = AddrModeFlat;
  s.AddrBStore.Offset = c.RsBSP;
  s.AddrBStore.Mode = AddrModeFlat;
  s.AddrStack.Offset = c.IntSp;
  s.AddrStack.Mode = AddrModeFlat;
#elif _M_ARM
  //https://github.com/JochenKalmbach/StackWalker/issues/26
  //https://docs.microsoft.com/en-us/cpp/build/overview-of-arm-abi-conventions?view=vs-2019
  imageType = IMAGE_FILE_MACHINE_ARM;
  s.AddrPC.Offset = c.Pc;
  s.AddrPC.Mode = AddrModeFlat;
  s.AddrFrame.Offset = c.R11;
  s.AddrFrame.Mode = AddrModeFlat;
  s.AddrStack.Offset = c.Sp;
  s.AddrStack.Mode = AddrModeFlat;
#elif _M_ARM64
  //https://github.com/JochenKalmbach/StackWalker/issues/26
  //https://docs.microsoft.com/en-us/cpp/build/arm64-windows-abi-conventions?view=vs-2019
  imageType = IMAGE_FILE_MACHINE_ARM64;
  s.AddrPC.Offset = c.Pc;
  s.AddrPC.Mode = AddrModeFlat;
  s.AddrFrame.Offset = c.Fp;
  s.AddrFrame.Mode = AddrModeFlat;
  s.AddrStack.Offset = c.Sp;
  s.AddrStack.Mode = AddrModeFlat;
#else
#error "Platform not supported!"
#endif

  pSym = (IMAGEHLP_SYMBOL64 *) malloc(sizeof(IMAGEHLP_SYMBOL64) + STACKWALK_MAX_NAMELEN);
  if (!pSym) goto cleanup;  // not enough memory...
  memset(pSym, 0, sizeof(IMAGEHLP_SYMBOL64) + STACKWALK_MAX_NAMELEN);
  pSym->SizeOfStruct = sizeof(IMAGEHLP_SYMBOL64);
  pSym->MaxNameLength = STACKWALK_MAX_NAMELEN;

  memset(&Line, 0, sizeof(Line));
  Line.SizeOfStruct = sizeof(Line);

  memset(&Module, 0, sizeof(Module));
  Module.SizeOfStruct = sizeof(Module);

  for (frameNum = 0; ; ++frameNum )
  {
    // get next stack frame (StackWalk64(), SymFunctionTableAccess64(), SymGetModuleBase64())
    // if this returns ERROR_INVALID_ADDRESS (487) or ERROR_NOACCESS (998), you can
    // assume that either you are done, or that the stack is so hosed that the next
    // deeper frame could not be found.
    // CONTEXT need not to be suplied if imageTyp is IMAGE_FILE_MACHINE_I386!
    if ( ! this->m_sw->pSW(imageType, this->m_hProcess, hThread, &s, &c, myReadProcMem, this->m_sw->pSFTA, this->m_sw->pSGMB, NULL) )
    {
      // INFO: "StackWalk64" does not set "GetLastError"...
      this->OnDbgHelpErr("StackWalk64", 0, s.AddrPC.Offset);
      break;
    }

    csEntry.offset = s.AddrPC.Offset;
    csEntry.name[0] = 0;
    csEntry.undName[0] = 0;
    csEntry.undFullName[0] = 0;
    csEntry.offsetFromSmybol = 0;
    csEntry.offsetFromLine = 0;
    csEntry.lineFileName[0] = 0;
    csEntry.lineNumber = 0;
    csEntry.loadedImageName[0] = 0;
    csEntry.moduleName[0] = 0;
    if (s.AddrPC.Offset == s.AddrReturn.Offset)
    {
      if ( (this->m_MaxRecursionCount > 0) && (curRecursionCount > m_MaxRecursionCount) )
      {
        this->OnDbgHelpErr("StackWalk64-Endless-Callstack!", 0, s.AddrPC.Offset);
        break;
      }
      curRecursionCount++;
    }
    else
      curRecursionCount = 0;
    if (s.AddrPC.Offset != 0)
    {
      // we seem to have a valid PC
      // show procedure info (SymGetSymFromAddr64())
      if (this->m_sw->pSGSFA(this->m_hProcess, s.AddrPC.Offset, &(csEntry.offsetFromSmybol), pSym) != FALSE)
      {
        MyStrCpy(csEntry.name, STACKWALK_MAX_NAMELEN, pSym->Name);
        // UnDecorateSymbolName()
        this->m_sw->pUDSN( pSym->Name, csEntry.undName, STACKWALK_MAX_NAMELEN, UNDNAME_NAME_ONLY );
        this->m_sw->pUDSN( pSym->Name, csEntry.undFullName, STACKWALK_MAX_NAMELEN, UNDNAME_COMPLETE );
      }
      else
      {
        this->OnDbgHelpErr("SymGetSymFromAddr64", GetLastError(), s.AddrPC.Offset);
      }

      // show line number info, NT5.0-method (SymGetLineFromAddr64())
      if (this->m_sw->pSGLFA != NULL )
      { // yes, we have SymGetLineFromAddr64()
        if (this->m_sw->pSGLFA(this->m_hProcess, s.AddrPC.Offset, &(csEntry.offsetFromLine), &Line) != FALSE)
        {
          csEntry.lineNumber = Line.LineNumber;
          MyStrCpy(csEntry.lineFileName, STACKWALK_MAX_NAMELEN, Line.FileName);
        }
        else
        {
          this->OnDbgHelpErr("SymGetLineFromAddr64", GetLastError(), s.AddrPC.Offset);
        }
      } // yes, we have SymGetLineFromAddr64()

      // show module info (SymGetModuleInfo64())
      if (this->m_sw->GetModuleInfo(this->m_hProcess, s.AddrPC.Offset, &Module ) != FALSE)
      { // got module info OK
        switch ( Module.SymType )
        {
        case SymNone:
          csEntry.symTypeString = "-nosymbols-";
          break;
        case SymCoff:
          csEntry.symTypeString = "COFF";
          break;
        case SymCv:
          csEntry.symTypeString = "CV";
          break;
        case SymPdb:
          csEntry.symTypeString = "PDB";
          break;
        case SymExport:
          csEntry.symTypeString = "-exported-";
          break;
        case SymDeferred:
          csEntry.symTypeString = "-deferred-";
          break;
        case SymSym:
          csEntry.symTypeString = "SYM";
          break;
#if API_VERSION_NUMBER >= 9
        case SymDia:
          csEntry.symTypeString = "DIA";
          break;
#endif
        case 8: //SymVirtual:
          csEntry.symTypeString = "Virtual";
          break;
        default:
          //_snprintf( ty, sizeof(ty), "symtype=%ld", (long) Module.SymType );
          csEntry.symTypeString = NULL;
          break;
        }

        MyStrCpy(csEntry.moduleName, STACKWALK_MAX_NAMELEN, Module.ModuleName);
        csEntry.baseOfImage = Module.BaseOfImage;
        MyStrCpy(csEntry.loadedImageName, STACKWALK_MAX_NAMELEN, Module.LoadedImageName);
      } // got module info OK
      else
      {
        this->OnDbgHelpErr("SymGetModuleInfo64", GetLastError(), s.AddrPC.Offset);
      }
    } // we seem to have a valid PC

    CallstackEntryType et = nextEntry;
    if (frameNum == 0)
      et = firstEntry;
    bLastEntryCalled = false;
    this->OnCallstackEntry(et, csEntry);

    if (s.AddrReturn.Offset == 0)
    {
      bLastEntryCalled = true;
      this->OnCallstackEntry(lastEntry, csEntry);
      SetLastError(ERROR_SUCCESS);
      break;
    }
  } // for ( frameNum )

  cleanup:
    if (pSym) free( pSym );

  if (bLastEntryCalled == false)
      this->OnCallstackEntry(lastEntry, csEntry);

  if (context == NULL)
    ResumeThread(hThread);

  return TRUE;
}

BOOL __stdcall StackWalker::myReadProcMem(
    HANDLE      hProcess,
    DWORD64     qwBaseAddress,
    PVOID       lpBuffer,
    DWORD       nSize,
    LPDWORD     lpNumberOfBytesRead
    )
{
  if (s_readMemoryFunction == NULL)
  {
    SIZE_T st;
    BOOL bRet = ReadProcessMemory(hProcess, (LPVOID) qwBaseAddress, lpBuffer, nSize, &st);
    *lpNumberOfBytesRead = (DWORD) st;
    //printf("ReadMemory: hProcess: %p, baseAddr: %p, buffer: %p, size: %d, read: %d, result: %d\n", hProcess, (LPVOID) qwBaseAddress, lpBuffer, nSize, (DWORD) st, (DWORD) bRet);
    return bRet;
  }
  else
  {
    return s_readMemoryFunction(hProcess, qwBaseAddress, lpBuffer, nSize, lpNumberOfBytesRead, s_readMemoryFunction_UserData);
  }
}

void StackWalker::OnLoadModule(LPCSTR img, LPCSTR mod, DWORD64 baseAddr, DWORD size, DWORD result, LPCSTR symType, LPCSTR pdbName, ULONGLONG fileVersion)
{
  CHAR buffer[STACKWALK_MAX_NAMELEN];
  if (fileVersion == 0)
    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s:%s (%p), size: %d (result: %d), SymType: '%s', PDB: '%s'\n", img, mod, (LPVOID) baseAddr, size, result, symType, pdbName);
  else
  {
    DWORD v4 = (DWORD) (fileVersion & 0xFFFF);
    DWORD v3 = (DWORD) ((fileVersion>>16) & 0xFFFF);
    DWORD v2 = (DWORD) ((fileVersion>>32) & 0xFFFF);
    DWORD v1 = (DWORD) ((fileVersion>>48) & 0xFFFF);
    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s:%s (%p), size: %d (result: %d), SymType: '%s', PDB: '%s', fileVersion: %d.%d.%d.%d\n", img, mod, (LPVOID) baseAddr, size, result, symType, pdbName, v1, v2, v3, v4);
  }
  OnOutput(buffer);
}

void StackWalker::OnCallstackEntry(CallstackEntryType eType, CallstackEntry &entry)
{
  CHAR buffer[STACKWALK_MAX_NAMELEN];
  if ( (eType != lastEntry) && (entry.offset != 0) )
  {
    if (entry.name[0] == 0)
      MyStrCpy(entry.name, STACKWALK_MAX_NAMELEN, "(function-name not available)");
    if (entry.undName[0] != 0)
      MyStrCpy(entry.name, STACKWALK_MAX_NAMELEN, entry.undName);
    if (entry.undFullName[0] != 0)
      MyStrCpy(entry.name, STACKWALK_MAX_NAMELEN, entry.undFullName);
    if (entry.lineFileName[0] == 0)
    {
      MyStrCpy(entry.lineFileName, STACKWALK_MAX_NAMELEN, "(filename not available)");
      if (entry.moduleName[0] == 0)
        MyStrCpy(entry.moduleName, STACKWALK_MAX_NAMELEN, "(module-name not available)");
      _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%p (%s): %s: %s\n", (LPVOID) entry.offset, entry.moduleName, entry.lineFileName, entry.name);
    }
    else
      _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s (%d): %s\n", entry.lineFileName, entry.lineNumber, entry.name);
    buffer[STACKWALK_MAX_NAMELEN-1] = 0;
    OnOutput(buffer);
  }
}

void StackWalker::OnDbgHelpErr(LPCSTR szFuncName, DWORD gle, DWORD64 addr)
{
  CHAR buffer[STACKWALK_MAX_NAMELEN];
  _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "ERROR: %s, GetLastError: %d (Address: %p)\n", szFuncName, gle, (LPVOID) addr);
  OnOutput(buffer);
}

void StackWalker::OnSymInit(LPCSTR szSearchPath, DWORD symOptions, LPCSTR szUserName)
{
  CHAR buffer[STACKWALK_MAX_NAMELEN];
  _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "SymInit: Symbol-SearchPath: '%s', symOptions: %d, UserName: '%s'\n", szSearchPath, symOptions, szUserName);
  OnOutput(buffer);
  // Also display the OS-version
  OSVERSIONINFOEXA ver;
  ZeroMemory(&ver, sizeof(OSVERSIONINFOEXA));
  ver.dwOSVersionInfoSize = sizeof(ver);
#pragma warning (disable : 4996)
  if (GetVersionExA( (OSVERSIONINFOA*) &ver) != FALSE)
  {
    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "OS-Version: %d.%d.%d (%s) 0x%x-0x%x\n",
      ver.dwMajorVersion, ver.dwMinorVersion, ver.dwBuildNumber,
      ver.szCSDVersion, ver.wSuiteMask, ver.wProductType);
    OnOutput(buffer);
  }
#pragma warning (default : 4996)
}

void StackWalker::OnOutput(LPCSTR buffer)
{
  OutputDebugStringA(buffer);
}

}

#pragma warning(pop)

#include "StringUtilities.h"

#include <vector>
#include <string>
#include <memory>
#include <exception>
#include <stdexcept>

#include <windows.h>
#include <stdio.h>
#include <stdlib.h>

namespace
{

const char *GetSehExpDescription(DWORD dwExpCode)
{
    switch (dwExpCode) {
    case EXCEPTION_ACCESS_VIOLATION:
        return "EXCEPTION_ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:
        return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT:
        return "EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT:
        return "EXCEPTION_DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND:
        return "EXCEPTION_FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:
        return "EXCEPTION_FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT:
        return "EXCEPTION_FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION:
        return "EXCEPTION_FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW:
        return "EXCEPTION_FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK:
        return "EXCEPTION_FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW:
        return "EXCEPTION_FLT_UNDERFLOW";
    case EXCEPTION_GUARD_PAGE:
        return "EXCEPTION_GUARD_PAGE";
    case EXCEPTION_ILLEGAL_INSTRUCTION:
        return "EXCEPTION_ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR:
        return "EXCEPTION_IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO:
        return "EXCEPTION_INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW:
        return "EXCEPTION_INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION:
        return "EXCEPTION_INVALID_DISPOSITION";
    case EXCEPTION_INVALID_HANDLE:
        return "EXCEPTION_INVALID_HANDLE";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION:
        return "EXCEPTION_NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION:
        return "EXCEPTION_PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP:
        return "EXCEPTION_SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW:
        return "EXCEPTION_STACK_OVERFLOW";
    case  STATUS_UNWIND_CONSOLIDATE:
        return "STATUS_UNWIND_CONSOLIDATE";
    default:
        return "UNKNOWN_SE_EXCEPTION";
    }
}

class ConciseStackWalker : public StackWalker
{
private:
    std::string &ExceptionInfo;
public:
    ConciseStackWalker(std::string &ExceptionInfo, DWORD dwExpCode) : StackWalker(), ExceptionInfo(ExceptionInfo)
    {
        ExceptionInfo = "";
        CHAR buffer[STACKWALK_MAX_NAMELEN] = {};
        _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "SEH ExceptionCode: %X(%s)\n", dwExpCode, GetSehExpDescription(dwExpCode));
        ExceptionInfo += systemToUtf8(buffer);
    }
    void OnSymInit(LPCSTR szSearchPath, DWORD symOptions, LPCSTR szUserName)
    {
    }
    void OnLoadModule(LPCSTR img, LPCSTR mod, DWORD64 baseAddr, DWORD size, DWORD result, LPCSTR symType, LPCSTR pdbName, ULONGLONG fileVersion)
    {
    }
    void OnCallstackEntry(CallstackEntryType eType, CallstackEntry &entry)
    {
        CHAR buffer[STACKWALK_MAX_NAMELEN] = {};
        if ((eType != lastEntry) && (entry.offset != 0)) {
            if (entry.name[0] == 0) {
                strcpy_s(entry.name, "(unknown)");
            }
            if (entry.undName[0] != 0) {
                strcpy_s(entry.name, entry.undName);
            }
            if (entry.undFullName[0] != 0) {
                strcpy_s(entry.name, entry.undFullName);
            }
            if (entry.lineFileName[0] == 0) {
                if (entry.moduleName[0] == 0) {
                    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "(%p)\n", (LPVOID)entry.offset);
                } else {
                    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s(%s.%p)\n", entry.name, entry.moduleName, (LPVOID)(entry.offset - entry.baseOfImage));
                }
            } else {
                _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s(%d) : %s(%s.%p)\n", entry.lineFileName, entry.lineNumber, entry.name, entry.moduleName, (LPVOID)(entry.offset - entry.baseOfImage));
            }
            OnOutput(buffer);
        }
    }
    void OnDbgHelpErr(LPCSTR szFuncName, DWORD gle, DWORD64 addr)
    {
        if ((szFuncName == "SymGetSymFromAddr64") && (gle == 487)) {
            return;
        }
        if ((szFuncName == "SymGetLineFromAddr64") && (gle == 487)) {
            return;
        }
        StackWalker::OnDbgHelpErr(szFuncName, gle, addr);
    }
    void OnOutput(LPCSTR szText)
    {
        ExceptionInfo += systemToUtf8(szText);
    }
};

class OneFrameStackWalker : public StackWalker
{
private:
    std::string &ExceptionInfo;
    int Skip;
public:
    OneFrameStackWalker(std::string &ExceptionInfo, int Skip) : StackWalker(), ExceptionInfo(ExceptionInfo), Skip(Skip)
    {
        ExceptionInfo = "";
    }
    void OnSymInit(LPCSTR szSearchPath, DWORD symOptions, LPCSTR szUserName)
    {
    }
    void OnLoadModule(LPCSTR img, LPCSTR mod, DWORD64 baseAddr, DWORD size, DWORD result, LPCSTR symType, LPCSTR pdbName, ULONGLONG fileVersion)
    {
    }
    void OnCallstackEntry(CallstackEntryType eType, CallstackEntry &entry)
    {
        if (Skip > 0)
        {
            Skip -= 1;
            return;
        }
        if (Skip < 0)
        {
            return;
        }
        Skip -= 1;
        CHAR buffer[STACKWALK_MAX_NAMELEN] = {};
        if ((eType != lastEntry) && (entry.offset != 0)) {
            if (entry.name[0] == 0) {
                strcpy_s(entry.name, "(unknown)");
            }
            if (entry.undName[0] != 0) {
                strcpy_s(entry.name, entry.undName);
            }
            if (entry.undFullName[0] != 0) {
                strcpy_s(entry.name, entry.undFullName);
            }
            if (entry.lineFileName[0] == 0) {
                if (entry.moduleName[0] == 0) {
                    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "(%p)\n", (LPVOID)entry.offset);
                } else {
                    _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s(%s.%p)\n", entry.name, entry.moduleName, (LPVOID)(entry.offset - entry.baseOfImage));
                }
            } else {
                _snprintf_s(buffer, STACKWALK_MAX_NAMELEN, "%s(%d) : %s(%s.%p)\n", entry.lineFileName, entry.lineNumber, entry.name, entry.moduleName, (LPVOID)(entry.offset - entry.baseOfImage));
            }
            OnOutput(buffer);
        }
    }
    void OnDbgHelpErr(LPCSTR szFuncName, DWORD gle, DWORD64 addr)
    {
    }
    void OnOutput(LPCSTR szText)
    {
        ExceptionInfo += systemToUtf8(szText);
    }
};

}

static thread_local std::string ThreadLocalExceptionInfo = "";

// Exception handling and stack-walking example:
static LONG WINAPI ExpFilter(EXCEPTION_POINTERS* pExp, DWORD dwExpCode)
{
    ConciseStackWalker sw(ThreadLocalExceptionInfo, dwExpCode);
    sw.ShowCallstack(GetCurrentThread(), pExp->ContextRecord);
    if (ThreadLocalExceptionInfo.size() > 0) {
        ThreadLocalExceptionInfo.pop_back();
    }
    return EXCEPTION_CONTINUE_SEARCH;
}

bool ExceptionStackTrace::IsDebuggerAttached()
{
    return IsDebuggerPresent() != 0;
}

std::string ExceptionStackTrace::GetStackTrace()
{
    return ThreadLocalExceptionInfo;
}

void *ExceptionStackTrace::detail::Execute(void *obj, void *(*f)(void *))
{
    __try
    {
        return f(obj);
    }
    __except (ExpFilter(GetExceptionInformation(), GetExceptionCode()))
    {
        return nullptr; //Will not be executed with EXCEPTION_CONTINUE_SEARCH
    }
}

std::string ExceptionStackTrace::GetStackFrame(int Skip)
{
    std::string s;
    OneFrameStackWalker sw(s, Skip + 2);
    sw.ShowCallstack();
    if (s.size() > 0) {
        return s.substr(0, s.size() - 1);
    } else {
        return s;
    }
}

#elif defined(__linux__) && !defined(__ANDROID__)

#include "AutoRelease.h"

#include <mutex>
#include <memory>
#include <sys/types.h>
#include <sys/wait.h>
#include <sys/ptrace.h>
#include <unistd.h>
#include <signal.h>
#include <dlfcn.h>
#include <execinfo.h>

static int gdb_check()
{
    int pid = fork();
    int status;
    int res;

    if (pid == -1)
    {
        perror("fork");
        return -1;
    }

    if (pid == 0)
    {
        int ppid = getppid();

        /* Child */
        if (ptrace(PTRACE_ATTACH, ppid, NULL, NULL) == 0)
        {
            /* Wait for the parent to stop and continue it */
            waitpid(ppid, NULL, 0);
            ptrace(PTRACE_CONT, NULL, NULL);

            /* Detach */
            ptrace(PTRACE_DETACH, getppid(), NULL, NULL);

            /* We were the tracers, so gdb is not present */
            res = 0;
        }
        else
        {
            /* Trace failed so gdb is present */
            res = 1;
        }
        exit(res);
    }
    else
    {
        waitpid(pid, &status, 0);
        res = WEXITSTATUS(status);
    }
    return res;
}

static std::mutex Lockee;
static std::shared_ptr<bool> GdbCheckResult;
static decltype(__cxa_throw) *__cxa_throw_original;

static thread_local bool ThreadLocalExceptionInfoEnabled = false;;
static thread_local std::string ThreadLocalExceptionInfo = "";

extern "C" void __cxa_throw(void *thrown_exception, void *pvtinfo, void(*dest)(void *))
{
    decltype(__cxa_throw) *_original = nullptr;
    {
        std::unique_lock<std::mutex> Lock(Lockee);
        if (__cxa_throw_original == nullptr)
        {
            __cxa_throw_original = (decltype(__cxa_throw) *)(dlsym(RTLD_NEXT, "__cxa_throw"));
        }
        _original = __cxa_throw_original;
    }

    if (ThreadLocalExceptionInfoEnabled)
    {
        void *array[10];
        size_t size;
        char **strings;

        size = backtrace(array, 10);
        strings = backtrace_symbols(array, size);

        std::string &ExceptionInfo = ThreadLocalExceptionInfo;
        ExceptionInfo = "";
        for (size_t i = 0; i < size; i += 1)
        {
            ExceptionInfo += strings[i];
            if (i != size - 1)
            {
                ExceptionInfo += "\n";
            }
        }
    }

    _original(thrown_exception, pvtinfo, dest);
    exit(EXIT_FAILURE);
}

static void sig_handler(int sig)
{
    struct sigaction sig_action = {};
    sig_action.sa_handler = SIG_DFL;
    sigemptyset(&sig_action.sa_mask);
    sigaction(SIGSEGV, &sig_action, nullptr);
    sigaction(SIGFPE, &sig_action, nullptr);

    if (sig == SIGSEGV)
    {
        throw "SIGSEGV";
    }
    else if (sig == SIGFPE)
    {
        throw "SIGFPE";
    }
    else
    {
        throw "UnknownSignal";
    }
}

bool ExceptionStackTrace::IsDebuggerAttached()
{
    std::unique_lock<std::mutex> Lock(Lockee);
    if (GdbCheckResult != nullptr)
    {
        return *GdbCheckResult;
    }
    auto b = gdb_check() == 1;
    GdbCheckResult = std::make_shared<bool>(b);
    return b;
}

std::string ExceptionStackTrace::GetStackTrace()
{
    return ThreadLocalExceptionInfo;
}

void *ExceptionStackTrace::detail::Execute(void *obj, void *(*f)(void *))
{
    uint8_t alternate_stack[SIGSTKSZ];

    stack_t oss = {};
    {
        stack_t ss = {};
        ss.ss_sp = (void *)alternate_stack;
        ss.ss_flags = 0;
        ss.ss_size = sizeof(alternate_stack);

        if (sigaltstack(&ss, &oss) != 0) { throw "UnexpectedReturnValue: sigaltstack"; }
    }

    struct sigaction old_sig_SEGV = {};
    struct sigaction old_sig_FPE = {};

    struct sigaction sig_action = {};
    sig_action.sa_handler = sig_handler;
    if (sigemptyset(&sig_action.sa_mask) != 0) { throw "UnexpectedReturnValue: sigemptyset"; }
    sig_action.sa_flags = SA_ONSTACK;

    if (sigaction(SIGSEGV, &sig_action, &old_sig_SEGV) != 0) { throw "UnexpectedReturnValue: sigaction"; }
    if (sigaction(SIGFPE, &sig_action, &old_sig_FPE) != 0) { throw "UnexpectedReturnValue: sigaction"; }

    ThreadLocalExceptionInfoEnabled = true;

    BaseSystem::AutoRelease ar([&]()
    {
        ThreadLocalExceptionInfoEnabled = false;

        sigaction(SIGSEGV, &old_sig_SEGV, nullptr);
        sigaction(SIGFPE, &old_sig_FPE, nullptr);

        sigaltstack(&oss, nullptr);
    });

    return f(obj);
}

std::string ExceptionStackTrace::GetStackFrame(int Skip)
{
    return "";
}

#else
#   warning "Platform not supported."

bool ExceptionStackTrace::IsDebuggerAttached()
{
    return false;
}

std::string ExceptionStackTrace::GetStackTrace()
{
    return "";
}

void *ExceptionStackTrace::detail::Execute(void *obj, void *(*f)(void *))
{
    return f(obj);
}

std::string ExceptionStackTrace::GetStackFrame(int Skip)
{
    return "";
}

#endif
