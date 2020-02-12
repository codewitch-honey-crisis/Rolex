﻿whereKeyword<id=501>= 'where'
overrideKeyword<id=500>= 'override'
constKeyword<id=499>= 'const'
abstractKeyword<id=498, terminal>= 'abstract'
virtualKeyword<id=497>= 'virtual'
staticKeyword<id=496>= 'static'
internalKeyword<id=495>= 'internal'
protectedKeyword<id=494>= 'protected'
privateKeyword<id=493>= 'private'
publicKeyword<id=492>= 'public'
eventKeyword<id=491>= 'event'
setKeyword<id=490>= 'set'
getKeyword<id=489>= 'get'
interfaceKeyword<id=488>= 'interface'
structKeyword<id=487>= 'struct'
enumKeyword<id=486>= 'enum'
classKeyword<id=485>= 'class'
partialKeyword<id=484>= 'partial'
voidType<id=483, terminal>= 'void'
assemblyKeyword<id=482>= 'assembly'
blockComment<id=481, terminal, blockEnd= "*/">= "/*"
lineComment<id=480, terminal>= '\/\/[^\r\n]*'
directive<id=479, terminal>= '#[A-Za-z]+[ \t]*[^\r\n]*'
colon<id=478>= ':'
varType<id=477>= 'var'
semi<id=476>= '\;'
finallyKeyword<id=475>= 'finally'
catchKeyword<id=474>= 'catch'
tryKeyword<id=473>= 'try'
returnKeyword<id=472>= 'return'
whileKeyword<id=471>= 'while'
throwKeyword<id=470>= 'throw'
forKeyword<id=469, terminal>= 'for'
elseKeyword<id=468>= 'else'
gotoKeyword<id=467>= 'goto'
ifKeyword<id=466>= 'if'
whitespace<id=465,hidden>= '[ \t\r\n\v\f]+'
dot<id=462>= '\.'
colonColon<id=461>= '::'
comma<id=460>= ','
rbrace<id=459>= '\}'
lbrace<id=458>= '\{'
rparen<id=457>= '\)'
lparen<id=456>= '\('
rbracket<id=455>= '\]'
lbracket<id=454>= '\['
not<id=453>= '!'
bitwiseOr<id=452>= '\|'
bitwiseOrAssign<id=451>= '\|='
or<id=450>= '\|\|'
bitwiseAnd<id=449>= '&'
bitwiseAndAssign<id=448>= '&='
and<id=447>= '&&'
mod<id=446>= '%'
modAssign<id=445>= '%='
div<id=444>= '/'
divAssign<id=443>= '/='
mul<id=442>= '\*'
mulAssign<id=441>= '\*='
sub<id=440>= '\-'
subAssign<id=439>= '\-='
dec<id=438>= '\-\-'
add<id=437>= '\+'
addAssign<id=436>= '\+='
inc<id=435>= '\+\+'
eq<id=434>= '='
notEq<id=433>= '!='
eqEq<id=432>= '=='
gt<id=431>= '\>'
gte<id=430>= '\>='
lt<id=429>= '\<'
lte<id=428>= '\<='
//characterLiteral<id=427>= '[\']((\\([\'\\"abfnrtv0]|[0-7]{3}|x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}))|[^\\\'])[\']'
characterLiteral<id=427>= '[\']([^\']|\\.)[\']'
//stringLiteral<id=426>= '"((\\([\'\\"abfnrtv0]|[0-7]{3}|x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}))|[^\\"])*"'
stringLiteral<id=426>= '"([^"]|\\.)*"'
//stringLiteral<id=426>= '"([^\\"\'\a\b\f\n\r\t\v\0]|\\[^\r\n]|\\[0-7]{3}|\\x[0-9A-Fa-f]{2}|\\u[0-9A-Fa-f]{4}|\\U[0-9A-Fa-f]{8})*"'
verbatimStringLiteral<id=424>= '@"([^"]|"")*"'
baseRef<id=423>= 'base'
thisRef<id=422>= 'this'
nullLiteral<id=421>= 'null'
boolLiteral<id=420>= 'true|false'
objectType<id=419>= 'object'
ulongType<id=418>= 'ulong'
longType<id=417>= 'long'
uintType<id=416>= 'uint'
intType<id=415>= 'int'
ushortType<id=414>= 'ushort'
shortType<id=413>= 'short'
byteType<id=412>= 'byte'
sbyteType<id=411>= 'sbyte'
decimalType<id=410>= 'decimal'
doubleType<id=409>= 'double'
floatType<id=408>= 'float'
charType<id=407>= 'char'
boolType<id=406>= 'bool'
stringType<id=405>= 'string'
globalKeyword<id=404>= 'global'
newKeyword<id=403>= 'new'
defaultOf<id=402>= 'default'
typeOf<id=401>= 'typeof'
refKeyword<id=400>= 'ref'
outKeyword<id=399>= 'out'
//verbatimIdentifier<id=398>= '@(_|[[:IsLetter:]])(_|[[:IsLetterOrDigit:]])*'
verbatimIdentifier<id=398>= '@[A-Z_a-z][0-9A-Z_a-z]*'
usingKeyword<id=397, terminal>= 'using'
namespaceKeyword<id=396, terminal>= 'namespace'
integerLiteral<id=463, priority= -50>= '(0x[0-9A-Fa-f]{1,16}|([0-9]+))([Uu][Ll]?|[Ll][Uu]?)?'
floatLiteral<id=464, priority= -51>= '(([0-9]+)(\.[0-9]+)?([Ee][+-]?[0-9]+)?[DdMmFf]?)|((\.[0-9]+)([Ee][+-]?[0-9]+)?[DdMmFf]?)'
//identifier<id=425, priority= -100>= '(_|[[:IsLetter:]])(_|[[:IsLetterOrDigit:]])*'
identifier<id=425, priority= -100>= '[A-Z_a-z][0-9A-Z_a-z]*'


