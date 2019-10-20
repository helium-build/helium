package dev.helium_build.docker

object WSApi {

  sealed trait Command

  final case class StopCommand
  (

  ) extends Command

}
