
ThisBuild / scalaVersion     := "2.13.1"
ThisBuild / version          := "0.1.0-SNAPSHOT"
ThisBuild / organization     := "com.example"
ThisBuild / organizationName := "example"
lazy val root = (project in file("."))
  .settings(
    name := "Helium",

    scalacOptions ++= Seq(
      "-deprecation",
      "-feature",
      "-language:higherKinds",
    ),

    addCompilerPlugin("org.typelevel" %% "kind-projector" % "0.10.3"),
    addCompilerPlugin("com.olegpy" %% "better-monadic-for" % "0.3.0"),

    resolvers += Resolver.sonatypeRepo("public"),

    mainClass in (Compile, run) := Some("dev.helium_build.Program"),
    mainClass in (Compile, packageBin) := Some("dev.helium_build.Program"),
    mainClass in assembly := Some("dev.helium_build.Program"),

    libraryDependencies ++= Seq(
      "org.scala-lang.modules" %% "scala-xml" % "1.2.0",

      "org.typelevel" %% "cats-core" % "2.0.0",
      "org.typelevel" %% "cats-effect" % "2.0.0",
      "org.typelevel" %% "cats-mtl-core" % "0.7.0",
      "org.typelevel" %% "kittens" % "2.0.0",

      "dev.zio" %% "zio" % "1.0.0-RC14",
      "dev.zio" %% "zio-streams" % "1.0.0-RC14",
      "dev.zio" %% "zio-interop-cats" % "2.0.0.0-RC5",
      
      "io.circe" %% "circe-core" % "0.12.2",
      "io.circe" %% "circe-generic" % "0.12.2",
      "io.circe" %% "circe-parser" % "0.12.2",

      "org.http4s"      %% "http4s-blaze-server" % "0.21.0-M5",
      "org.http4s"      %% "http4s-blaze-client" % "0.21.0-M5",
      "org.http4s"      %% "http4s-circe"        % "0.21.0-M5",
      "org.http4s"      %% "http4s-dsl"          % "0.21.0-M5",
      "co.fs2" %% "fs2-io" % "2.0.1",

      "tech.sparse" %%  "toml-scala" % "0.2.1",

      "com.softwaremill.sttp.client" %% "core" % "2.0.0-M6",
      "com.softwaremill.sttp.client" %% "async-http-client-backend-zio" % "2.0.0-M6",
      "com.softwaremill.sttp.client" %% "async-http-client-backend-cats" % "2.0.0-M6",
      "com.softwaremill.sttp.client" %% "circe" % "2.0.0-M6",

      "org.scalatra.scalate" % "scalate-core_2.13" % "1.9.5",

      "commons-io" % "commons-io" % "2.6",
      "org.apache.commons" % "commons-compress" % "1.18",
      "commons-codec" % "commons-codec" % "1.13",
      "org.apache.commons" % "commons-lang3" % "3.9",

      "com.github.zafarkhaja" % "java-semver" % "0.9.0",

      "org.slf4j" % "slf4j-api" % "1.7.28",
      "org.slf4j" % "slf4j-log4j12" % "1.7.28",


      "org.scalatest" %% "scalatest" % "3.0.8" % Test,
    ),

    assemblyMergeStrategy in assembly := {
      case x if x.contains("io.netty.versions.properties") => MergeStrategy.discard
      case "zio/BuildInfo$.class" => MergeStrategy.discard
      case x =>
        val oldStrategy = (assemblyMergeStrategy in assembly).value
        oldStrategy(x)
    },

  )

