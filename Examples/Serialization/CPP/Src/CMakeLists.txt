cmake_minimum_required(VERSION 2.8)
find_package(Threads)
add_definitions("-std=c++17")
include_directories("../Lib/utf8/source")
include_directories(".")

file(GLOB_RECURSE SRCS RELATIVE ${CMAKE_CURRENT_SOURCE_DIR} "*.cpp")
string(REGEX REPLACE "CMakeFiles/[^;]+;?" "" SRCS "${SRCS}")
add_executable(DataCopy ${SRCS})
target_link_libraries(DataCopy ${CMAKE_THREAD_LIBS_INIT})
