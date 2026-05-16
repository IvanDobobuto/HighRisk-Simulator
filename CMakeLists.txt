cmake_minimum_required(VERSION 3.20)

project(HighRiskSimulator LANGUAGES NONE)

set(DOTNET_CONFIGURATION "Release" CACHE STRING "Build configuration: Debug or Release")
set(DOTNET_RUNTIME "" CACHE STRING "Optional runtime identifier: linux-x64, osx-x64, osx-arm64, win-x64")
set(DOTNET_SELF_CONTAINED "false" CACHE STRING "Publish self-contained: true or false")

set(SOLUTION_FILE "${CMAKE_SOURCE_DIR}/HighRiskSimulator.sln")
set(APP_PROJECT "${CMAKE_SOURCE_DIR}/HighRiskSimulator/HighRiskSimulator.csproj")
set(PUBLISH_DIR "${CMAKE_BINARY_DIR}/publish")

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

add_custom_target(restore
    COMMAND ${DOTNET_EXECUTABLE} restore "${SOLUTION_FILE}"
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Restoring NuGet packages"
)

add_custom_target(build
    COMMAND ${DOTNET_EXECUTABLE} build "${SOLUTION_FILE}" --configuration ${DOTNET_CONFIGURATION} --no-restore
    DEPENDS restore
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Building HighRisk Simulator"
)

add_custom_target(run
    COMMAND ${DOTNET_EXECUTABLE} run --project "${APP_PROJECT}" --configuration ${DOTNET_CONFIGURATION}
    DEPENDS build
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Running HighRisk Simulator"
)

if(DOTNET_RUNTIME STREQUAL "")
    add_custom_target(publish
        COMMAND ${DOTNET_EXECUTABLE} publish "${APP_PROJECT}"
            --configuration ${DOTNET_CONFIGURATION}
            --output "${PUBLISH_DIR}"
            --self-contained ${DOTNET_SELF_CONTAINED}
        DEPENDS restore
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
        COMMENT "Publishing HighRisk Simulator"
    )
else()
    add_custom_target(publish
        COMMAND ${DOTNET_EXECUTABLE} publish "${APP_PROJECT}"
            --configuration ${DOTNET_CONFIGURATION}
            --runtime ${DOTNET_RUNTIME}
            --output "${PUBLISH_DIR}"
            --self-contained ${DOTNET_SELF_CONTAINED}
        DEPENDS restore
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
        COMMENT "Publishing HighRisk Simulator for ${DOTNET_RUNTIME}"
    )
endif()

add_custom_target(clean-dotnet
    COMMAND ${DOTNET_EXECUTABLE} clean "${SOLUTION_FILE}" --configuration ${DOTNET_CONFIGURATION}
    COMMAND ${CMAKE_COMMAND} -E remove_directory "${PUBLISH_DIR}"
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Cleaning HighRisk Simulator build outputs"
)