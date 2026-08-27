@echo off
chcp 65001 >nul

set "output_cs=cs_files_content.txt"
set "output_cpp=cpp_files_content.txt"
set "output_h=h_files_content.txt"

echo 正在导出 .cs、.cpp 和 .h 文件内容...
echo.

:: 处理 .cs 文件
> "%output_cs%" (
    for /R %%f in (*.cs) do (
        echo.
        echo 【%%f】
        type "%%f"
        echo.
        echo ========================================
    )
)

:: 处理 .cpp 文件
> "%output_cpp%" (
    for /R %%f in (*.cpp) do (
        echo.
        echo 【%%f】
        type "%%f"
        echo.
        echo ========================================
    )
)

:: 处理 .h 文件
> "%output_h%" (
    for /R %%f in (*.h) do (
        echo.
        echo 【%%f】
        type "%%f"
        echo.
        echo ========================================
    )
)

echo 导出完成！已递归处理 %cd% 及其子目录下的 .cs、.cpp 和 .h 文件
echo 输出文件：
echo   - %output_cs%
echo   - %output_cpp%
echo   - %output_h%
pause